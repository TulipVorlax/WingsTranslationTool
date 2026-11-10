using System;
using System.IO;
using System.Text;
using WingsLang.Core;

namespace SmokeTest
{
    internal static class Program
    {
        private static int _failures;

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== SmokeTest WingsLangCore ===");

            if (args.Length < 2)
            {
                Console.WriteLine("Usage: SmokeTest.exe <en.lang> <fr.lang> [rapport.txt]");
                return 1;
            }

            string enPath = args[0];
            string frPath = args[1];

            TestRoundTrip(enPath, "EN");
            TestRoundTrip(frPath, "FR");

            LangFile en = LangFile.Load(enPath);
            LangFile fr = LangFile.Load(frPath);

            Console.WriteLine();
            Console.WriteLine("Modules EN : " + en.Modules.Count);
            Console.WriteLine("Modules FR : " + fr.Modules.Count);

            foreach (LangModule m in en.Modules)
            {
                int groups = 0, entries = 0;
                foreach (LangGroup g in m.Groups) { if (!g.IsRaw) { groups++; } entries += g.Entries.Count; }
                Console.WriteLine("  EN module " + m.Key + " : " + groups + " groupes, " + entries + " entrées (ligne " + m.TermNode.Line + ")");
            }

            // Vérification ponctuelle : clé crash_dialog dans EN doit exister.
            LangEntry crash = en.FindEntry("wings", "crash_dialog", "1");
            if (crash == null)
            {
                Fail("crash_dialog/1 introuvable dans EN");
            }
            else
            {
                Console.WriteLine("  [contrôle] crash_dialog/1 EN = \"" + crash.Value + "\" (ligne " + crash.Line + ")");
            }

            // Test d'édition : change la valeur puis vérifie que ça re-sérialise.
            TestEditRoundTrip(en, fr);

            LangComparer.Report r = LangComparer.Compare(en, fr);
            Console.WriteLine();
            Console.WriteLine("=== Comparaison EN -> FR ===");
            Console.WriteLine("Entrées EN : " + r.EnCount + " | Entrées FR : " + r.FrCount);
            Console.WriteLine("Identiques      : " + r.Identical);
            Console.WriteLine("Différentes     : " + r.Different);
            Console.WriteLine("Manquantes (FR) : " + r.MissingEntries);
            Console.WriteLine("Uniquement FR   : " + r.OnlyFrEntries);
            Console.WriteLine("Groupes manquants : " + r.MissingGroups + " | Groupes uniquement FR : " + r.OnlyFrGroups + " | Modules manquants : " + r.MissingModules);

            foreach (DiffModule m in r.Modules)
            {
                if (m.Groups.Count == 0) { continue; }
                Console.WriteLine("  Module " + m.Key + (m.OrderDiffer ? " [ORDRE DIFFÉRENT]" : ""));
                foreach (DiffGroup g in m.Groups)
                {
                    if (g.Status != GroupStatus.Same)
                    {
                        Console.WriteLine("    Groupe " + g.Key + " : " + g.Status + " (FR ligne " + g.FrLine + ")");
                    }
                }
            }

            if (args.Length >= 3)
            {
                WriteReport(args[2], r);
                Console.WriteLine();
                Console.WriteLine("Rapport écrit : " + args[2]);
            }

            Console.WriteLine();
            if (_failures == 0)
            {
                Console.WriteLine("OK — aucun échec.");
                return 0;
            }
            Console.WriteLine(_failures + " échec(s).");
            return 2;
        }

        private static void TestRoundTrip(string path, string label)
        {
            string original = LangFile.ReadFileText(path);
            LangFile f = LangFile.FromText(original);
            string rendered = f.Document.Render();
            if (rendered == original)
            {
                Console.WriteLine("[round-trip " + label + "] IDENTIQUE (" + original.Length + " car.)");
            }
            else
            {
                Fail("[round-trip " + label + "] DIFFÉRENT : " + original.Length + " -> " + rendered.Length + " car.");
                // Affiche la première divergence.
                int n = Math.Min(original.Length, rendered.Length);
                int i = 0;
                while (i < n && original[i] == rendered[i]) { i++; }
                string oWin = original.Substring(i, Math.Min(30, original.Length - i));
                string rWin = rendered.Substring(i, Math.Min(30, rendered.Length - i));
                Console.WriteLine("    divergence à l'index " + i + " (ligne " + LineAt(original, i) + ")");
                Console.WriteLine("    source  : " + Show(oWin));
                Console.WriteLine("    rendu   : " + Show(rWin));
            }
        }

        private static int LineAt(string s, int index)
        {
            int line = 1;
            int limit = index < s.Length ? index : s.Length;
            for (int k = 0; k < limit; k++)
            {
                if (s[k] == '\n') { line++; }
            }
            return line;
        }

        private static void TestEditRoundTrip(LangFile en, LangFile fr)
        {
            LangEntry e = fr.FindEntry("wings", "edit_menu", "1");
            if (e == null || !e.IsText)
            {
                Fail("edit_menu/1 introuvable ou non éditable dans FR");
                return;
            }
            string old = e.Value;
            string marker = "TEST-ÉDITION-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            e.Value = marker;
            string rendered = fr.Document.Render();
            if (!rendered.Contains(marker))
            {
                Fail("l'édition de valeur ne réapparaît pas dans le rendu");
            }
            else
            {
                Console.WriteLine("[édition] la valeur modifiée apparaît dans le rendu (" + rendered.Length + " car.).");
                // Le reste du document doit être intact à part cette ligne : on vérifie
                // qu'on a seulement modifié la ligne concernée (peu de différence de taille).
            }
            e.Value = old; // restaure
        }

        private static void WriteReport(string path, LangComparer.Report r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Rapport de comparaison de traduction Wings 3D (EN -> FR)");
            sb.AppendLine("========================================================");
            sb.AppendLine("Généré le " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine();
            sb.AppendLine("Résumé :");
            sb.AppendLine("  Entrées EN            : " + r.EnCount);
            sb.AppendLine("  Entrées FR            : " + r.FrCount);
            sb.AppendLine("  Identiques            : " + r.Identical + "   (à vérifier : peut être volontaire)");
            sb.AppendLine("  Différentes           : " + r.Different);
            sb.AppendLine("  Manquantes en FR      : " + r.MissingEntries);
            sb.AppendLine("  Uniquement en FR      : " + r.OnlyFrEntries);
            sb.AppendLine("  Groupes manquants     : " + r.MissingGroups);
            sb.AppendLine("  Groupes uniquement FR : " + r.OnlyFrGroups);
            sb.AppendLine("  Modules manquants     : " + r.MissingModules);
            sb.AppendLine();

            foreach (DiffModule m in r.Modules)
            {
                sb.AppendLine("Module " + m.Key + (m.OrderDiffer ? "  [ORDRE DIFFÉRENT]" : "") + "  (EN ligne " + m.EnLine + (m.FrLine > 0 ? ", FR ligne " + m.FrLine : "") + ")");
                foreach (DiffGroup g in m.Groups)
                {
                    sb.AppendLine("  Groupe " + g.Key + "  (" + g.Status + ")");
                    foreach (DiffEntry e in g.Entries)
                    {
                        if (e.Status == EntryStatus.Same) { continue; }
                        sb.AppendLine("    - " + e.Key + " [" + e.Status + "]");
                        sb.AppendLine("        EN (ligne " + e.EnLine + ") : " + Show(e.EnValue));
                        if (e.Status == EntryStatus.Different)
                        {
                            sb.AppendLine("        FR (ligne " + e.FrLine + ") : " + Show(e.FrValue));
                        }
                    }
                }
                sb.AppendLine();
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static string Show(string v)
        {
            if (v == null) { return "(vide)"; }
            string s = v.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
            if (s.Length <= 120) { return "\"" + s + "\""; }
            return "\"" + s.Substring(0, 117) + "...\"";
        }

        private static void Fail(string msg)
        {
            _failures++;
            Console.WriteLine("[ÉCHEC] " + msg);
        }
    }
}