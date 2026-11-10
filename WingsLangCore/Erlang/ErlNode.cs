using System;
using System.Collections.Generic;
using System.Text;

namespace WingsLang.Core
{
    public enum ErlNodeKind
    {
        Tuple,
        List,
        Binary,
        Atom,
        QuotedAtom,
        Integer,
        Float,
        String,
        Other
    }

    /// <summary>
    /// Nœud d'un terme Erlang. Un nœud dispose, soit d'un texte source exact (Text),
    /// soit d'une structure (Children + ElementTrivia) utilisée pour la ré-émission.
    /// Le round-trip est exact tant qu'aucun descendant n'est modifié.
    /// </summary>
    public sealed class ErlNode
    {
        public ErlNodeKind Kind;

        /// <summary>Index de début (inclus) dans le texte source d'origine.</summary>
        public int Start = -1;

        /// <summary>Index de fin (exclus) dans le texte source d'origine.</summary>
        public int End = -1;

        /// <summary>Ligne de début dans le document (1-based).</summary>
        public int Line = 1;

        /// <summary>Colonne de début (1-based).</summary>
        public int Col = 1;

        /// <summary>
        /// Texte source exact du nœud. Null si le nœud doit être régénéré
        /// structuralement (nœud créé ou descendant modifié).
        /// </summary>
        public string Text;

        /// <summary>Vrai si ce nœud ou l'un de ses descendants a été modifié.</summary>
        public bool SubtreeDirty;

        public List<ErlNode> Children = new List<ErlNode>();

        /// <summary>
        /// Pour les conteneurs : trivia textuels autour des enfants.
        /// ElementTrivia[0] précède le premier enfant ; ElementTrivia[i] sépare
        /// l'enfant i-1 de l'enfant i ; le dernier élément précède la parenthèse fermante.
        /// Comprend les virgules. Longueur = Children.Count + 1.
        /// </summary>
        public List<string> ElementTrivia = new List<string>();

        /// <summary>Valeur décodée pour les scalaires (String, QuotedAtom, Atom, Integer).</summary>
        public string Value;

        public long IntValue;
        public double FloatValue;

        public ErlNode Parent;

        public bool IsContainer
        {
            get { return Kind == ErlNodeKind.Tuple || Kind == ErlNodeKind.List || Kind == ErlNodeKind.Binary; }
        }

        /// <summary>Marque ce nœud et ses ancêtres comme modifiés.</summary>
        public void SetDirty()
        {
            for (ErlNode n = this; n != null && !n.SubtreeDirty; n = n.Parent)
            {
                n.SubtreeDirty = true;
            }
        }

        /// <summary>
        /// Copie exacte (verbatim) de ce nœud et de sa sous-structure, fondée sur le texte
        /// source. À utiliser pour dupliquer un morceau de document en conservant les octets.
        /// </summary>
        public ErlNode CloneVerbatim()
        {
            var n = new ErlNode
            {
                Kind = Kind,
                Start = Start,
                End = End,
                Line = Line,
                Col = Col,
                Text = Text,
                Value = Value,
                IntValue = IntValue,
                FloatValue = FloatValue,
                SubtreeDirty = SubtreeDirty
            };
            foreach (ErlNode c in Children)
            {
                ErlNode cc = c.CloneVerbatim();
                cc.Parent = n;
                n.Children.Add(cc);
            }
            n.ElementTrivia.AddRange(ElementTrivia);
            return n;
        }

        /// <summary>Émet le nœud (texte verbatim si intact, sinon reconstruction structurelle).</summary>
        public string Render()
        {
            if (!SubtreeDirty && Text != null)
            {
                return Text;
            }
            switch (Kind)
            {
                case ErlNodeKind.String:
                    return ErlEscape.EncodeString(Value ?? string.Empty);
                case ErlNodeKind.QuotedAtom:
                    return ErlEscape.EncodeAtom(Value ?? string.Empty);
                case ErlNodeKind.Atom:
                    return ErlEscape.EncodeAtom(Value ?? string.Empty);
                case ErlNodeKind.Integer:
                    return IntValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case ErlNodeKind.Float:
                    return FormatFloat(FloatValue);
                case ErlNodeKind.Tuple:
                    return RenderContainer("{", "}");
                case ErlNodeKind.List:
                    return RenderContainer("[", "]");
                case ErlNodeKind.Binary:
                case ErlNodeKind.Other:
                default:
                    return Text ?? string.Empty;
            }
        }

        private static string FormatFloat(double v)
        {
            string s = v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (s.IndexOf('.') < 0 && s.IndexOf('E') < 0 && s.IndexOf('e') < 0)
            {
                s += ".0";
            }
            return s;
        }

        private string RenderContainer(string open, string close)
        {
            var sb = new StringBuilder();
            sb.Append(open);
            int n = Children.Count;
            for (int i = 0; i <= n; i++)
            {
                string trivia = (i < ElementTrivia.Count) ? ElementTrivia[i] : string.Empty;
                sb.Append(trivia);
                if (i < n)
                {
                    sb.Append(Children[i].Render());
                }
            }
            sb.Append(close);
            return sb.ToString();
        }
    }
}