using System.Collections.Generic;

namespace WingsLang.Core
{
    public enum EntryStatus
    {
        Same,            // présent des deux côtés, valeurs identiques
        Different,       // présent des deux côtés, valeurs différentes
        MissingInTarget, // présent dans le fichier de référence (EN), absent de la cible (FR)
        OnlyInTarget     // présent seulement dans la cible (FR)
    }

    public enum GroupStatus
    {
        Same,
        MissingInTarget,
        OnlyInTarget
    }

    public sealed class DiffEntry
    {
        public string ModuleKey;
        public string GroupKey;
        public string Key;
        public EntryStatus Status;
        public string EnValue = string.Empty;
        public string FrValue = string.Empty;
        public int EnLine;
        public int FrLine;
        public LangEntry EnEntry;
        public LangEntry FrEntry;

        /// <summary>Vrai si l'entrée FR peut être éditée (chaîne modifiable).</summary>
        public bool CanEdit { get { return FrEntry != null && FrEntry.IsText; } }
    }

    public sealed class DiffGroup
    {
        public string ModuleKey;
        public string Key;
        public GroupStatus Status;
        public int EnLine;
        public int FrLine;
        public List<DiffEntry> Entries = new List<DiffEntry>();
    }

    public sealed class DiffModule
    {
        public string Key;
        public bool OrderDiffer;
        public int EnLine;
        public int FrLine;
        public List<DiffGroup> Groups = new List<DiffGroup>();
    }

    /// <summary>
    /// Comparateur EN (référence) / FR (traduction). Traite plusieurs termes par fichier
    /// (les blocs « strings have no translation » sont fusionnés dans les modules).
    /// </summary>
    public static class LangComparer
    {
        public sealed class Report
        {
            public List<DiffModule> Modules = new List<DiffModule>();
            public int EnCount;
            public int FrCount;
            public int MissingEntries;
            public int OnlyFrEntries;
            public int Identical;
            public int Different;
            public int MissingGroups;
            public int OnlyFrGroups;
            public int MissingModules;

            public int TotalEntries
            {
                get { return MissingEntries + OnlyFrEntries + Identical + Different; }
            }
        }

        public static Report Compare(LangFile en, LangFile fr)
        {
            var report = new Report();
            report.EnCount = CountEntries(en);
            report.FrCount = CountEntries(fr);

            var frModules = new Dictionary<string, LangModule>();
            foreach (LangModule m in fr.Modules)
            {
                if (!frModules.ContainsKey(m.Key)) { frModules.Add(m.Key, m); }
            }

            var seenFr = new HashSet<string>();

            foreach (LangModule em in en.Modules)
            {
                LangModule fm;
                if (frModules.TryGetValue(em.Key, out fm))
                {
                    seenFr.Add(fm.Key);
                    CompareModule(em, fm, report);
                }
                else
                {
                    CompareModule(em, null, report);
                    report.MissingModules++;
                }
            }

            foreach (LangModule fm in fr.Modules)
            {
                if (!seenFr.Contains(fm.Key))
                {
                    CompareModule(null, fm, report);
                }
            }

            return report;
        }

        private static void CompareModule(LangModule en, LangModule fr, Report report)
        {
            var dm = new DiffModule
            {
                Key = en != null ? en.Key : fr.Key,
                EnLine = en != null && en.TermNode != null ? en.TermNode.Line : 0,
                FrLine = fr != null && fr.TermNode != null ? fr.TermNode.Line : 0
            };

            if (fr == null)
            {
                // Module entier absent de FR.
                foreach (LangGroup eg in en.Groups)
                {
                    if (eg.IsRaw) { continue; }
                    var dg = new DiffGroup { ModuleKey = en.Key, Key = eg.Key, Status = GroupStatus.MissingInTarget, EnLine = eg.Line, FrLine = 0 };
                    foreach (LangEntry ee in eg.Entries)
                    {
                        dg.Entries.Add(MakeEntry(ee, null, EntryStatus.MissingInTarget));
                        report.MissingEntries++;
                    }
                    dm.Groups.Add(dg);
                    report.MissingGroups++;
                }
                report.Modules.Add(dm);
                return;
            }

            if (en == null)
            {
                // Module présent uniquement en FR.
                foreach (LangGroup fg in fr.Groups)
                {
                    if (fg.IsRaw) { continue; }
                    var dg = new DiffGroup { ModuleKey = fr.Key, Key = fg.Key, Status = GroupStatus.OnlyInTarget, EnLine = 0, FrLine = fg.Line };
                    foreach (LangEntry fe in fg.Entries)
                    {
                        dg.Entries.Add(MakeEntry(null, fe, EntryStatus.OnlyInTarget));
                        report.OnlyFrEntries++;
                    }
                    dm.Groups.Add(dg);
                    report.OnlyFrGroups++;
                }
                report.Modules.Add(dm);
                return;
            }

            // Ordre des groupes FR (pour détecter les différences d'ordre).
            var frGroupPos = new Dictionary<string, int>();
            int fp = 0;
            foreach (LangGroup fg in fr.Groups)
            {
                if (!fg.IsRaw && !frGroupPos.ContainsKey(fg.Key)) { frGroupPos.Add(fg.Key, fp++); }
            }

            // Groupes FR absents du module EN.
            foreach (LangGroup fg in fr.Groups)
            {
                if (fg.IsRaw || (en != null && en.FindGroup(fg.Key) != null)) { continue; }
                var dg = new DiffGroup { ModuleKey = fr.Key, Key = fg.Key, Status = GroupStatus.OnlyInTarget, EnLine = 0, FrLine = fg.Line };
                foreach (LangEntry fe in fg.Entries)
                {
                    dg.Entries.Add(MakeEntry(null, fe, EntryStatus.OnlyInTarget));
                    report.OnlyFrEntries++;
                }
                dm.Groups.Add(dg);
                report.OnlyFrGroups++;
            }

            // Groupes EN, dans l'ordre d'apparition.
            int enPos = 0;
            foreach (LangGroup eg in en.Groups)
            {
                if (eg.IsRaw) { continue; }
                LangGroup fg = fr.FindGroup(eg.Key);
                int enIndex = enPos++;

                var dg = new DiffGroup { ModuleKey = en.Key, Key = eg.Key, EnLine = eg.Line };
                if (fg != null)
                {
                    dg.FrLine = fg.Line;
                    dg.Status = GroupStatus.Same;
                    int frIndex;
                    if (frGroupPos.TryGetValue(fg.Key, out frIndex) && frIndex != enIndex)
                    {
                        dm.OrderDiffer = true;
                    }
                }
                else
                {
                    dg.Status = GroupStatus.MissingInTarget;
                    report.MissingGroups++;
                }

                foreach (LangEntry ee in eg.Entries)
                {
                    LangEntry fe = fg == null ? null : fg.FindEntry(ee.Key);
                    if (fe == null)
                    {
                        dg.Entries.Add(MakeEntry(ee, null, EntryStatus.MissingInTarget));
                        report.MissingEntries++;
                    }
                    else
                    {
                        bool same = SameValue(ee, fe);
                        dg.Entries.Add(MakeEntry(ee, fe, same ? EntryStatus.Same : EntryStatus.Different));
                        if (same) { report.Identical++; } else { report.Different++; }
                    }
                }

                if (fg != null)
                {
                    foreach (LangEntry fe in fg.Entries)
                    {
                        if (eg.FindEntry(fe.Key) == null)
                        {
                            dg.Entries.Add(MakeEntry(null, fe, EntryStatus.OnlyInTarget));
                            report.OnlyFrEntries++;
                        }
                    }
                }

                dm.Groups.Add(dg);
            }

            report.Modules.Add(dm);
        }

        private static bool SameValue(LangEntry a, LangEntry b)
        {
            return string.Equals(a.Value, b.Value, System.StringComparison.Ordinal);
        }

        private static DiffEntry MakeEntry(LangEntry en, LangEntry fr, EntryStatus status)
        {
            var e = new DiffEntry
            {
                Status = status,
                EnEntry = en,
                FrEntry = fr,
                ModuleKey = (en ?? fr).ModuleKey,
                GroupKey = (en ?? fr).GroupKey,
                Key = (en ?? fr).Key
            };
            if (en != null)
            {
                e.EnValue = en.Value;
                e.EnLine = en.Line;
            }
            if (fr != null)
            {
                e.FrValue = fr.Value;
                e.FrLine = fr.Line;
            }
            return e;
        }

        private static int CountEntries(LangFile f)
        {
            int n = 0;
            foreach (LangModule m in f.Modules)
            {
                foreach (LangGroup g in m.Groups)
                {
                    n += g.Entries.Count;
                }
            }
            return n;
        }
    }
}