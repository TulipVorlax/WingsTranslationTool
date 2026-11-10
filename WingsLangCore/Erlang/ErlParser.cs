using System;

namespace WingsLang.Core
{
    /// <summary>
    /// Analyseur descendant pour des fichiers de termes Erlang (listes, tuples,
    /// scalaires, binaires). Gère plusieurs termes par document, séparés par des points.
    /// </summary>
    public sealed class ErlParser
    {
        private readonly string _s;
        private readonly ErlLexer _lx;

        public ErlParser(string text)
        {
            _s = text;
            _lx = new ErlLexer(text);
        }

        public ErlDocument ParseAll()
        {
            var doc = new ErlDocument { Source = _s };
            bool have = _lx.Next();
            doc.HeaderTrivia = have ? _lx.Trivia : string.Empty;

            while (have && _lx.Kind != ErlTok.End)
            {
                ErlNode node = ParseNode();

                var term = new ErlTopTerm { Node = node };
                HaveDotOrEnd(ref term);
                doc.Terms.Add(term);

                // Trivie après le point ; si fin de fichier, le reste du texte est dans Trivia.
                have = _lx.Next();
                term.Trailing = _lx.Trivia;
            }
            return doc;
        }

        /// <summary>
        /// Avance vers le point de fin de terme ; capture la trivie avant le point dans term.BeforeDot
        /// et positionne term.EndLine sur la ligne du point (ou du dernier jeton si introuvable).
        /// </summary>
        private void HaveDotOrEnd(ref ErlTopTerm term)
        {
            _lx.Next();
            term.BeforeDot = _lx.Trivia;
            term.EndLine = _lx.Line;
            if (_lx.Kind != ErlTok.Dot)
            {
                // Terme sans point final : on tolère et on s'arrête après ce terme.
                term.EndLine = term.Node.End >= 0 ? LineOf(term.Node.End) : term.Node.Line;
            }
            // On restaure le jeton courant comme étant le point/dernier jeton consommé,
            // pour que l'appelant puisse ensuite avancer en toute sécurité.
        }

        private int LineOf(int index)
        {
            int line = 1;
            int limit = index < _s.Length ? index : _s.Length;
            for (int i = 0; i < limit; i++)
            {
                if (_s[i] == '\n') { line++; }
            }
            return line;
        }

        private ErlNode ParseNode()
        {
            switch (_lx.Kind)
            {
                case ErlTok.LBrace:
                    return ParseContainer(ErlNodeKind.Tuple, ErlTok.RBrace, "{", "}");
                case ErlTok.LBracket:
                    return ParseContainer(ErlNodeKind.List, ErlTok.RBracket, "[", "]");
                case ErlTok.LBin:
                    return ParseBinary();
                case ErlTok.String:
                    return Scalar(ErlNodeKind.String);
                case ErlTok.QuotedAtom:
                    return Scalar(ErlNodeKind.QuotedAtom);
                case ErlTok.Integer:
                    return Scalar(ErlNodeKind.Integer);
                case ErlTok.Float:
                    return Scalar(ErlNodeKind.Float);
                case ErlTok.Atom:
                    return Scalar(ErlNodeKind.Atom);
                default:
                    return Scalar(ErlNodeKind.Other);
            }
        }

        private ErlNode Scalar(ErlNodeKind kind)
        {
            var n = new ErlNode
            {
                Kind = kind,
                Start = _lx.Start,
                End = _lx.End,
                Line = _lx.Line,
                Col = _lx.Col,
                Value = _lx.Value,
                IntValue = _lx.IntValue,
                FloatValue = _lx.FloatValue
            };
            n.Text = _s.Substring(n.Start, n.End - n.Start);
            return n;
        }

        /// <summary>
        /// Binaire <<...>> traité comme opaque (aucune structure) : on consomme les jetons
        /// jusqu'à la fermeture, en tolérant les << imbriqués.
        /// </summary>
        private ErlNode ParseBinary()
        {
            var n = new ErlNode { Kind = ErlNodeKind.Binary, Start = _lx.Start, Line = _lx.Line, Col = _lx.Col };
            int depth = 1;
            _lx.Next();
            while (depth > 0 && _lx.Kind != ErlTok.End)
            {
                if (_lx.Kind == ErlTok.LBin) { depth++; }
                else if (_lx.Kind == ErlTok.RBin) { depth--; }
                if (depth == 0) { break; }
                _lx.Next();
            }
            n.End = _lx.End;
            n.Text = _s.Substring(n.Start, n.End - n.Start);
            return n;
        }

        private ErlNode ParseContainer(ErlNodeKind kind, ErlTok closeKind, string open, string close)
        {
            var n = new ErlNode { Kind = kind, Start = _lx.Start, Line = _lx.Line, Col = _lx.Col };
            _lx.Next();

            while (true)
            {
                if (_lx.Kind == closeKind || _lx.Kind == ErlTok.End)
                {
                    break;
                }
                if (_lx.Kind == ErlTok.Comma || _lx.Kind == ErlTok.Pipe)
                {
                    _lx.Next(); // tolérant : virgule/pipeline isolé(s)
                    continue;
                }
                ErlNode child = ParseNode();
                child.Parent = n;
                n.Children.Add(child);
                _lx.Next();
                if (_lx.Kind == ErlTok.Comma || _lx.Kind == ErlTok.Pipe)
                {
                    _lx.Next();
                }
                else if (_lx.Kind == closeKind || _lx.Kind == ErlTok.End)
                {
                    break;
                }
            }

            n.End = _lx.End;
            n.Text = _s.Substring(n.Start, n.End - n.Start);

            // Découpes des trivies autour des enfants (index absolus dans le source).
            int cnt = n.Children.Count;
            int closeStart = _lx.Start;
            n.ElementTrivia = new System.Collections.Generic.List<string>(cnt + 1);
            for (int i = 0; i <= cnt; i++)
            {
                int a, b;
                if (i == 0)
                {
                    a = n.Start + open.Length;
                    b = (cnt == 0) ? closeStart : n.Children[0].Start;
                }
                else if (i == cnt)
                {
                    a = n.Children[cnt - 1].End;
                    b = closeStart;
                }
                else
                {
                    a = n.Children[i - 1].End;
                    b = n.Children[i].Start;
                }
                n.ElementTrivia.Add(_s.Substring(a, b - a));
            }
            return n;
        }
    }
}