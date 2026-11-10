using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WingsLang.Core
{
    /// <summary>Un groupe nommé dans un module : {Clé, [{CléEntrée, "valeur"}]}.</summary>
    public sealed class LangGroup
    {
        public string Key;
        public string ModuleKey;
        public ErlNode KeyNode;
        public ErlNode ValueNode;   // nœud valeur du groupe (normalement une liste)
        public ErlNode ListNode;    // nœud liste contenant les entrées (si tableau)
        public List<LangEntry> Entries = new List<LangEntry>();

        public int Line { get { return KeyNode == null ? 1 : KeyNode.Line; } }
        public bool IsRaw { get { return KeyNode == null; } }

        public LangEntry FindEntry(string key)
        {
            foreach (LangEntry e in Entries)
            {
                if (e.Key == key) { return e; }
            }
            return null;
        }
    }

    /// <summary>Une entrée de traduction : {Clé, "valeur"}.</summary>
    public sealed class LangEntry
    {
        public string Key;
        public string ModuleKey;
        public string GroupKey;
        public ErlNode TupleNode;
        public ErlNode KeyNode;
        public ErlNode ValueNode;   // nœud valeur (normalement une chaîne)

        public int Line { get { return KeyNode == null ? 1 : KeyNode.Line; } }

        /// <summary>Vrai si la valeur est une chaîne éditable (pas un atome, entier, binaire...).</summary>
        public bool IsText { get { return ValueNode != null && ValueNode.Kind == ErlNodeKind.String; } }

        /// <summary>Valeur décodée (ou texte brut si non-chaîne).</summary>
        public string Value
        {
            get
            {
                if (ValueNode == null) { return string.Empty; }
                switch (ValueNode.Kind)
                {
                    case ErlNodeKind.String:
                    case ErlNodeKind.QuotedAtom:
                    case ErlNodeKind.Integer:
                    case ErlNodeKind.Atom:
                        return ValueNode.Value ?? string.Empty;
                    default:
                        return ValueNode.Text ?? string.Empty;
                }
            }
            set
            {
                if (ValueNode == null || ValueNode.Kind != ErlNodeKind.String)
                {
                    throw new InvalidOperationException("Cette entrée n'est pas une chaîne éditable.");
                }
                ValueNode.Value = value;
                ValueNode.SetDirty();
            }
        }
    }

    /// <summary>Un module : {NomModule, [{Groupe, ...}]}.</summary>
    public sealed class LangModule
    {
        public string Key;
        public ErlNode TermNode;
        public ErlNode ListNode;
        public List<LangGroup> Groups = new List<LangGroup>();

        public LangGroup FindGroup(string key)
        {
            foreach (LangGroup g in Groups)
            {
                if (!g.IsRaw && g.Key == key) { return g; }
            }
            return null;
        }
    }

    /// <summary>
    /// Un fichier .lang Wings 3D. Conçu pour un round-trip exact : tant qu'aucune valeur
    /// n'est modifiée, Save() reproduit les octets d'origine. Les modifications de valeurs
    /// ne touchent que les morceaux concernés.
    /// </summary>
    public sealed class LangFile
    {
        public string FilePath = string.Empty;
        public ErlDocument Document = new ErlDocument();
        public List<LangModule> Modules = new List<LangModule>();

        public static LangFile Load(string path)
        {
            string text = ReadFileText(path);
            var f = new LangFile();
            f.FilePath = path;
            f.Document = new ErlParser(text).ParseAll();
            f.Rebuild();
            return f;
        }

        public static LangFile FromText(string text)
        {
            var f = new LangFile();
            f.Document = new ErlParser(text).ParseAll();
            f.Rebuild();
            return f;
        }

        public LangModule FindModule(string key)
        {
            foreach (LangModule m in Modules)
            {
                if (m.Key == key) { return m; }
            }
            return null;
        }

        public LangEntry FindEntry(string moduleKey, string groupKey, string entryKey)
        {
            LangModule m = FindModule(moduleKey);
            if (m == null) { return null; }
            LangGroup g = m.FindGroup(groupKey);
            if (g == null) { return null; }
            return g.FindEntry(entryKey);
        }

        public void Save()
        {
            Save(FilePath);
        }

        public void Save(string path)
        {
            byte[] data = new UTF8Encoding(false).GetBytes(Document.Render());
            File.WriteAllBytes(path, data);
        }

        /// <summary>
        /// Charge le texte d'un fichier en tolérant UTF-8 ou Latin-1 (fichiers anciens).
        /// </summary>
        public static string ReadFileText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(28591).GetString(bytes);
            }
        }

        /// <summary>Reconstruit les wrappers Modules/Groups/Entries depuis l'arbre du document.</summary>
        public void Rebuild()
        {
            Modules.Clear();

            foreach (ErlTopTerm t in Document.Terms)
            {
                ErlNode n = t.Node;
                if (n == null || n.Kind != ErlNodeKind.Tuple || n.Children.Count < 2) { continue; }

                ErlNode keyNode = n.Children[0];
                if (!IsScalarKey(keyNode)) { continue; }
                string moduleKey = ScalarText(keyNode);

                LangModule m = FindModule(moduleKey);
                if (m == null)
                {
                    m = new LangModule { Key = moduleKey, TermNode = n };
                    Modules.Add(m);
                }
                if (n.Children[1].Kind == ErlNodeKind.List)
                {
                    m.ListNode = n.Children[1];
                }

                ErlNode body = n.Children[1];
                if (body.Kind != ErlNodeKind.List) { continue; }

                foreach (ErlNode gnode in body.Children)
                {
                    if (gnode.Kind == ErlNodeKind.Tuple && gnode.Children.Count >= 2 && IsScalarKey(gnode.Children[0]))
                    {
                        string gKey = ScalarText(gnode.Children[0]);
                        LangGroup g = m.FindGroup(gKey);
                        if (g == null)
                        {
                            g = new LangGroup { Key = gKey, ModuleKey = moduleKey, KeyNode = gnode.Children[0], ValueNode = gnode.Children[1] };
                            if (gnode.Children[1].Kind == ErlNodeKind.List)
                            {
                                g.ListNode = gnode.Children[1];
                            }
                            m.Groups.Add(g);
                        }
                        BuildEntries(g, gnode);
                    }
                    else
                    {
                        m.Groups.Add(new LangGroup { Key = "« brut »", ModuleKey = moduleKey, ValueNode = gnode });
                    }
                }
            }
        }

        private static void BuildEntries(LangGroup g, ErlNode groupNode)
        {
            ErlNode value = groupNode.Children[1];
            if (value == null || value.Kind != ErlNodeKind.List) { return; }

            foreach (ErlNode enode in value.Children)
            {
                if (enode.Kind != ErlNodeKind.Tuple || enode.Children.Count < 2) { continue; }
                ErlNode ekey = enode.Children[0];
                if (!IsScalarKey(ekey)) { continue; }
                string eKey = ScalarText(ekey);
                if (g.FindEntry(eKey) != null) { continue; } // doublon : on garde la première occurrence
                g.Entries.Add(new LangEntry
                {
                    Key = eKey,
                    ModuleKey = g.ModuleKey,
                    GroupKey = g.Key,
                    TupleNode = enode,
                    KeyNode = ekey,
                    ValueNode = enode.Children[1]
                });
            }
        }

        internal static bool IsScalarKey(ErlNode n)
        {
            switch (n.Kind)
            {
                case ErlNodeKind.Atom:
                case ErlNodeKind.QuotedAtom:
                case ErlNodeKind.Integer:
                case ErlNodeKind.String:
                    return true;
                default:
                    return false;
            }
        }

        internal static string ScalarText(ErlNode n)
        {
            switch (n.Kind)
            {
                case ErlNodeKind.Atom:
                case ErlNodeKind.QuotedAtom:
                case ErlNodeKind.Integer:
                case ErlNodeKind.String:
                    return n.Value ?? string.Empty;
                default:
                    return n.Text ?? string.Empty;
            }
        }

        // ---------------------------------------------------------------------
        // Ajout des entrées manquantes (traduction depuis le fichier de référence).
        // ---------------------------------------------------------------------

        /// <summary>
        /// Fait muter ce fichier pour y ajouter toutes les entrées manquantes de
        /// <paramref name="src"/> (modules, groupes et entrées clonés verbatim). Retourne
        /// le nombre d'entrées ajoutées.
        /// </summary>
        public int AddMissingFrom(LangFile src)
        {
            int added = 0;
            foreach (LangModule sm in src.Modules)
            {
                LangModule tm = FindModule(sm.Key);
                foreach (LangGroup sg in sm.Groups)
                {
                    if (tm == null)
                    {
                        // Module entier absent : on le crée avec les groupes encore absents.
                        ErlNode groupClone = GroupTupleOf(sg).CloneVerbatim();
                        ErlNode termNode = BuildModuleNode(sm.Key, groupClone);
                        Document.Terms.Add(new ErlTopTerm { Node = termNode, BeforeDot = "", Trailing = "\n" });
                        added += sg.Entries.Count;
                    }
                    else
                    {
                        LangGroup tg = tm.FindGroup(sg.Key);
                        if (tg == null)
                        {
                            AppendChildToList(tm.ListNode, GroupTupleOf(sg).CloneVerbatim());
                            added += sg.Entries.Count;
                        }
                        else
                        {
                            foreach (LangEntry se in sg.Entries)
                            {
                                if (tg.FindEntry(se.Key) == null)
                                {
                                    AppendChildToList(tg.ListNode, se.TupleNode.CloneVerbatim());
                                    added++;
                                }
                            }
                        }
                    }
                }
            }
            Rebuild();
            return added;
        }

        /// <summary>Ajoute une seule entrée manquante, clonée depuis <paramref name="src"/>.</summary>
        public LangEntry AddMissingEntry(LangEntry src)
        {
            LangModule tm = FindModule(src.ModuleKey);
            if (tm == null)
            {
                ErlNode groupTuple = src.TupleNode.Parent == null ? null : src.TupleNode.Parent.Parent;
                ErlNode groupClone = (groupTuple ?? src.TupleNode).CloneVerbatim();
                ErlNode termNode = BuildModuleNode(src.ModuleKey, groupClone);
                Document.Terms.Add(new ErlTopTerm { Node = termNode, BeforeDot = "", Trailing = "\n" });
            }
            else
            {
                LangGroup tg = tm.FindGroup(src.GroupKey);
                if (tg == null)
                {
                    ErlNode groupTuple = src.TupleNode.Parent == null ? null : src.TupleNode.Parent.Parent;
                    AppendChildToList(tm.ListNode, (groupTuple ?? src.TupleNode).CloneVerbatim());
                }
                else if (tg.FindEntry(src.Key) == null)
                {
                    AppendChildToList(tg.ListNode, src.TupleNode.CloneVerbatim());
                }
            }
            Rebuild();
            return FindEntry(src.ModuleKey, src.GroupKey, src.Key);
        }

        private static ErlNode GroupTupleOf(LangGroup sg)
        {
            return sg.ValueNode.Parent;
        }

        /// <summary>
        /// Construit un nœud de terme {module, [{groupe, [...]}]} : le groupe est cloné
        /// verbatim, le terme reçoit une trivie canonique.
        /// </summary>
        private static ErlNode BuildModuleNode(string moduleName, ErlNode groupTupleClone)
        {
            ErlNode atomNode = new ErlNode { Kind = ErlNodeKind.Atom, Value = moduleName, SubtreeDirty = true };
            groupTupleClone.Parent = null;

            var listNode = new ErlNode { Kind = ErlNodeKind.List, SubtreeDirty = true };
            listNode.ElementTrivia.Add("\n ");
            listNode.ElementTrivia.Add("\n ");
            groupTupleClone.Parent = listNode;
            listNode.Children.Add(groupTupleClone);

            var tuple = new ErlNode { Kind = ErlNodeKind.Tuple, SubtreeDirty = true };
            tuple.ElementTrivia.Add(string.Empty);
            tuple.ElementTrivia.Add("\n ");
            atomNode.Parent = tuple;
            listNode.Parent = tuple;
            tuple.Children.Add(atomNode);
            tuple.Children.Add(listNode);
            return tuple;
        }

        /// <summary>
        /// Ajoute <paramref name="child"/> à la fin de la liste <paramref name="listNode"/>,
        /// en réutilisant le style d'indentation de la liste et en conservant la trivie finale.
        /// </summary>
        internal static void AppendChildToList(ErlNode listNode, ErlNode child)
        {
            if (listNode == null) { return; }
            string indent = IndentOf(listNode);

            if (listNode.Children.Count == 0)
            {
                string old = listNode.ElementTrivia.Count > 0 ? listNode.ElementTrivia[0] : string.Empty;
                listNode.ElementTrivia.Clear();
                listNode.ElementTrivia.Add("\n" + indent);
                listNode.ElementTrivia.Add(old);
                child.Line = listNode.Line + LineCount("\n" + indent);
            }
            else
            {
                string lastTrivia = listNode.ElementTrivia[listNode.ElementTrivia.Count - 1];
                listNode.ElementTrivia.RemoveAt(listNode.ElementTrivia.Count - 1);
                string sep = ",\n" + indent;
                listNode.ElementTrivia.Add(sep);
                listNode.ElementTrivia.Add(lastTrivia);
                var last = listNode.Children[listNode.Children.Count - 1];
                child.Line = last.Line + LineCount(sep);
            }

            child.Parent = listNode;
            listNode.Children.Add(child);
            listNode.SetDirty();
        }

        internal static string IndentOf(ErlNode listNode)
        {
            if (listNode.ElementTrivia.Count > 0)
            {
                string t = listNode.ElementTrivia[0];
                int nl = t.LastIndexOf('\n');
                if (nl >= 0)
                {
                    return t.Substring(nl + 1);
                }
            }
            return "  ";
        }

        internal static int LineCount(string s)
        {
            int n = 0;
            foreach (char c in s)
            {
                if (c == '\n') { n++; }
            }
            return n;
        }
    }
}