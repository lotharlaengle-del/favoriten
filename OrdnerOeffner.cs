// Ordner-Öffner: nimmt Links der Form  ordner:<Pfad, URL-kodiert>  entgegen und
// öffnet den Ordner im Windows-Explorer.
//
//   - Kein Explorer offen      -> neues Fenster mit dem Ordner, im Vordergrund
//   - Explorer schon offen     -> dieses Fenster nach vorn, neuer Tab, dorthin
//   - Tab klappt nicht         -> Rückfall auf ein neues Fenster
//
// Sicherheit: Jede Webseite kann einen ordner:-Link auslösen. Deshalb werden
// ausschließlich vorhandene Ordner auf Laufwerksbuchstaben geöffnet - nie
// Dateien, nie Programme, keine Netzwerkpfade (\\Server\... würde Windows
// sonst veranlassen, sich bei einem fremden Server anzumelden).
//
// Bewusst C# 5 (Windows PowerShell 5.1 übersetzt es beim Installieren).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

public static class OrdnerOeffner
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);

    const int SW_RESTORE = 9;
    const byte VK_CONTROL = 0x11, VK_T = 0x54;
    const uint KEYUP = 0x0002;

    class Tab { public IntPtr Hwnd; public object Obj; }

    [STAThread]
    public static int Main(string[] args)
    {
        string pfad = null;
        try { pfad = PfadAusArgument(string.Join(" ", args)); } catch { }

        if (pfad == null) { Meldung("Das ist kein gültiger Ordnerlink.\n\n" + string.Join(" ", args)); return 1; }
        if (pfad.StartsWith(@"\\")) { Meldung("Netzwerkpfade werden aus Sicherheitsgründen nicht geöffnet:\n\n" + pfad); return 1; }
        if (!Directory.Exists(pfad)) { Meldung("Ordner nicht gefunden:\n\n" + pfad); return 2; }

        bool erledigt = false;
        try { erledigt = InTabOeffnen(pfad); } catch { }
        if (!erledigt) NeuesFenster(pfad);
        return 0;
    }

    // "ordner:C%3A%5CUsers%5C..."  ->  "C:\Users\..."
    static string PfadAusArgument(string a)
    {
        a = a.Trim().Trim('"');
        int i = a.IndexOf(':');
        if (i < 0 || !a.Substring(0, i).Equals("ordner", StringComparison.OrdinalIgnoreCase)) return null;

        string p = Uri.UnescapeDataString(a.Substring(i + 1)).Trim().Trim('"');
        p = Environment.ExpandEnvironmentVariables(p).Replace('/', '\\');
        if (p.Length == 0 || p.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return null;

        if (p.StartsWith(@"\\")) return p;                         // wird oben abgelehnt
        bool laufwerk = p.Length >= 3 && char.IsLetter(p[0]) && p[1] == ':' && p[2] == '\\';
        if (!laufwerk) return null;

        p = Path.GetFullPath(p);
        if (p.Length > 3) p = p.TrimEnd('\\');
        return p;
    }

    // ---------------------------------------------------------------- Tabs

    static bool InTabOeffnen(string pfad)
    {
        Type typ = Type.GetTypeFromProgID("Shell.Application");
        if (typ == null) return false;
        object shell = Activator.CreateInstance(typ);

        List<Tab> vorher = ExplorerTabs(shell);
        if (vorher.Count == 0) return false;

        IntPtr ziel = ObersterExplorer(vorher);
        if (ziel == IntPtr.Zero) return false;
        int tabsVorher = 0;
        foreach (Tab t in vorher) if (t.Hwnd == ziel) tabsVorher++;

        // Fenster nach vorn. Wir wurden vom Browser auf einen Klick hin
        // gestartet und dürfen deshalb den Vordergrund wechseln.
        if (IsIconic(ziel)) ShowWindow(ziel, SW_RESTORE);
        SetForegroundWindow(ziel);
        if (!WarteBis(delegate { return GetForegroundWindow() == ziel; }, 1500)) return false;

        // Neuen Tab anfordern (Strg+T) - eine offizielle Schnittstelle gibt es nicht.
        Thread.Sleep(60);
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_T, 0, 0, UIntPtr.Zero);
        keybd_event(VK_T, 0, KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYUP, UIntPtr.Zero);

        object neu = null;
        WarteBis(delegate
        {
            List<Tab> jetzt = ExplorerTabs(shell);
            List<Tab> imZiel = new List<Tab>();
            foreach (Tab t in jetzt) if (t.Hwnd == ziel) imZiel.Add(t);
            if (imZiel.Count <= tabsVorher) return false;

            // Bevorzugt: der Tab, den es vorher nicht gab. Sonst der zuletzt gemeldete.
            foreach (Tab t in imZiel)
            {
                bool bekannt = false;
                foreach (Tab v in vorher) if (ReferenceEquals(v.Obj, t.Obj)) { bekannt = true; break; }
                if (!bekannt) { neu = t.Obj; break; }
            }
            if (neu == null) neu = imZiel[imZiel.Count - 1].Obj;
            return true;
        }, 3000);

        if (neu == null) return false;
        ((dynamic)neu).Navigate2(pfad);
        return true;
    }

    static List<Tab> ExplorerTabs(object shell)
    {
        List<Tab> liste = new List<Tab>();
        dynamic fenster = ((dynamic)shell).Windows();
        int n = fenster.Count;
        for (int i = 0; i < n; i++)
        {
            try
            {
                dynamic w = fenster.Item(i);
                if (w == null) continue;
                string exe = (string)w.FullName;
                if (exe == null || !Path.GetFileName(exe).Equals("explorer.exe", StringComparison.OrdinalIgnoreCase)) continue;
                Tab t = new Tab();
                t.Hwnd = new IntPtr(Convert.ToInt64(w.HWND));
                t.Obj = (object)w;
                liste.Add(t);
            }
            catch { }
        }
        return liste;
    }

    // Das Explorerfenster, das in der Stapelreihenfolge am weitesten oben liegt
    // (also das zuletzt benutzte).
    static IntPtr ObersterExplorer(List<Tab> tabs)
    {
        HashSet<IntPtr> menge = new HashSet<IntPtr>();
        foreach (Tab t in tabs) menge.Add(t.Hwnd);
        IntPtr gefunden = IntPtr.Zero;
        EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            if (menge.Contains(h) && IsWindowVisible(h)) { gefunden = h; return false; }
            return true;
        }, IntPtr.Zero);
        return gefunden;
    }

    // ------------------------------------------------------------ Rückfälle

    static void NeuesFenster(string pfad)
    {
        // Ein Laufwerk ("C:\") nicht in Anführungszeichen: \" würde als Maskierung gelesen.
        string arg = pfad.Length <= 3 ? pfad : "\"" + pfad + "\"";
        Process.Start(new ProcessStartInfo("explorer.exe", arg) { UseShellExecute = true });
    }

    static bool WarteBis(Func<bool> bedingung, int ms)
    {
        Stopwatch uhr = Stopwatch.StartNew();
        while (uhr.ElapsedMilliseconds < ms)
        {
            try { if (bedingung()) return true; } catch { }
            Thread.Sleep(50);
        }
        return false;
    }

    static void Meldung(string text)
    {
        MessageBox.Show(text, "Ordner-Öffner", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
    }
}
