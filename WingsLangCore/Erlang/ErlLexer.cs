using System;

namespace WingsLang.Core
{
    /// <summary>
    /// Types de jetons du tokeniseur Erlang (sous-ensemble suffisant pour les fichiers .lang).
    /// </summary>
    public enum ErlTok
    {
        End,
        LBrace,
        RBrace,
        LBracket,
        RBracket,
        LBin,
        RBin,
        Comma,
        Pipe,
        Dot,
        Atom,
        QuotedAtom,
        Integer,
        Float,
        String,
        Other
    }

    /// <summary>
    /// Tokeniseur pour les termes Erlang. Avance dans le texte en gérant les commentaires
    /// (% jusqu'à la fin de ligne), les positions ligne/colonne et la trivie (espaces +
    /// commentaires) qui précède chaque jeton. Les chaînes et atomes quotés sont décodés.
    /// </summary>
    public sealed class ErlLexer
    {
        private readonly string _s;
        private int _p;
        private int _line = 1;
        private int _col = 1;

        public string Newline = "\n";

        /// <summary>Espaces et commentaires accumulés depuis le jeton précédent.</summary>
        public string Trivia;

        public ErlTok Kind;
        public int Start;   // index absolu du premier caractère du jeton
        public int End;     // index exclusif de fin du jeton
        public int Line;    // ligne de début (1-based)
        public int Col;     // colonne de début (1-based)

        public string Value;      // valeur décodée pour String / QuotedAtom / Atom
        public long IntValue;
        public double FloatValue;

        public ErlLexer(string text)
        {
            _s = text;
        }

        /// <summary>
        /// Passe au jeton suivant. Retourne false si fin de fichier (Kind = End).
        /// Trivia contient le texte (espaces et commentaires) situé avant ce jeton.
        /// </summary>
        public bool Next()
        {
            Trivia = string.Empty;
            while (_p < _s.Length)
            {
                char c = _s[_p];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    Trivia += c;
                    Consume();
                }
                else if (c == '%')
                {
                    while (_p < _s.Length && _s[_p] != '\n' && _s[_p] != '\r')
                    {
                        Trivia += _s[_p];
                        Consume();
                    }
                }
                else
                {
                    break;
                }
            }

            if (_p >= _s.Length)
            {
                Kind = ErlTok.End;
                Start = End = _p;
                return false;
            }

            Start = _p;
            Line = _line;
            Col = _col;
            Value = null;

            char first = _s[_p];
            switch (first)
            {
                case '{': Consume(); Kind = ErlTok.LBrace; break;
                case '}': Consume(); Kind = ErlTok.RBrace; break;
                case '[': Consume(); Kind = ErlTok.LBracket; break;
                case ']': Consume(); Kind = ErlTok.RBracket; break;
                case ',': Consume(); Kind = ErlTok.Comma; break;
                case '|': Consume(); Kind = ErlTok.Pipe; break;
                case '.':
                    Consume();
                    Kind = ErlTok.Dot;
                    break;
                case '<':
                    if (Peek(1) == '<') { Consume(); Consume(); Kind = ErlTok.LBin; }
                    else { Consume(); Kind = ErlTok.Other; }
                    break;
                case '>':
                    if (Peek(1) == '>') { Consume(); Consume(); Kind = ErlTok.RBin; }
                    else { Consume(); Kind = ErlTok.Other; }
                    break;
                case '"': ReadString(); break;
                case '\'': ReadQuotedAtom(); break;
                default:
                    if (char.IsDigit(first))
                    {
                        ReadNumber(false);
                    }
                    else if (first == '-' && _p + 1 < _s.Length && char.IsDigit(_s[_p + 1]))
                    {
                        ReadNumber(true);
                    }
                    else if (char.IsLetter(first) || first == '_')
                    {
                        ReadAtom();
                    }
                    else
                    {
                        Consume();
                        Kind = ErlTok.Other;
                    }
                    break;
            }

            End = _p;
            return true;
        }

        private char Peek(int offset)
        {
            int idx = _p + offset;
            if (idx < 0 || idx >= _s.Length) { return '\0'; }
            return _s[idx];
        }

        private void Consume()
        {
            char c = _s[_p];
            _p++;
            if (c == '\n') { _line++; _col = 1; }
            else if (c == '\r') { _col = 1; }
            else { _col++; }
        }

        private void ReadString()
        {
            Consume(); // guillemet ouvrant
            var body = new System.Text.StringBuilder();
            while (_p < _s.Length)
            {
                char c = _s[_p];
                if (c == '"')
                {
                    Consume();
                    break;
                }
                if (c == '\\')
                {
                    CopyEscape(body);
                }
                else
                {
                    body.Append(c);
                    Consume();
                }
            }
            Kind = ErlTok.String;
            Value = ErlEscape.Decode(body.ToString());
        }

        private void ReadQuotedAtom()
        {
            Consume(); // apostrophe d'ouverture
            var body = new System.Text.StringBuilder();
            while (_p < _s.Length)
            {
                char c = _s[_p];
                if (c == '\'')
                {
                    Consume();
                    break;
                }
                if (c == '\\')
                {
                    CopyEscape(body);
                }
                else
                {
                    body.Append(c);
                    Consume();
                }
            }
            Kind = ErlTok.QuotedAtom;
            Value = ErlEscape.Decode(body.ToString());
        }

        /// <summary>
        /// Recopie cru une séquence d'échappement (antislash + suite) dans le corps,
        /// en s'arrêtant exactement à la fin de l'échappement (hex, octal, \x{...}, \^X).
        /// </summary>
        private void CopyEscape(System.Text.StringBuilder body)
        {
            body.Append('\\');
            Consume();
            if (_p >= _s.Length) { return; }
            char e = _s[_p];
            body.Append(e);
            Consume();
            switch (e)
            {
                case 'x':
                    if (_p < _s.Length && _s[_p] == '{')
                    {
                        body.Append('{');
                        Consume();
                        while (_p < _s.Length && _s[_p] != '}') { body.Append(_s[_p]); Consume(); }
                        if (_p < _s.Length) { body.Append('}'); Consume(); }
                    }
                    else
                    {
                        int n = 0;
                        while (_p < _s.Length && n < 2 && IsHex(_s[_p]))
                        {
                            body.Append(_s[_p]);
                            Consume();
                            n++;
                        }
                    }
                    break;
                case '^':
                    if (_p < _s.Length) { body.Append(_s[_p]); Consume(); }
                    break;
                default:
                    if (e >= '0' && e <= '7')
                    {
                        int n = 0;
                        while (_p < _s.Length && n < 2 && _s[_p] >= '0' && _s[_p] <= '7')
                        {
                            body.Append(_s[_p]);
                            Consume();
                            n++;
                        }
                    }
                    break;
            }
        }

        private static bool IsHex(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private void ReadNumber(bool neg)
        {
            if (neg) { Consume(); }
            while (_p < _s.Length && char.IsDigit(_s[_p])) { Consume(); }

            bool isFloat = false;
            if (_p < _s.Length && _s[_p] == '.' && _p + 1 < _s.Length && char.IsDigit(_s[_p + 1]))
            {
                isFloat = true;
                Consume(); // point décimal
                while (_p < _s.Length && char.IsDigit(_s[_p])) { Consume(); }
            }

            if (_p < _s.Length && (_s[_p] == 'e' || _s[_p] == 'E'))
            {
                int save = _p;
                Consume();
                if (_p < _s.Length && (_s[_p] == '+' || _s[_p] == '-')) { Consume(); }
                if (_p < _s.Length && char.IsDigit(_s[_p]))
                {
                    isFloat = true;
                    while (_p < _s.Length && char.IsDigit(_s[_p])) { Consume(); }
                }
                else
                {
                    _p = save; // pas un exposant ; on le laisse au token suivant
                }
            }

            string text = _s.Substring(Start, _p - Start);
            Value = text;
            if (isFloat)
            {
                Kind = ErlTok.Float;
                double v;
                if (!double.TryParse(text, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out v))
                {
                    v = 0.0;
                }
                FloatValue = v;
            }
            else
            {
                Kind = ErlTok.Integer;
                long v;
                if (!long.TryParse(text, out v)) { v = 0; }
                IntValue = v;
            }
        }

        private void ReadAtom()
        {
            while (_p < _s.Length)
            {
                char c = _s[_p];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '@')
                {
                    Consume();
                }
                else
                {
                    break;
                }
            }
            Kind = ErlTok.Atom;
            Value = _s.Substring(Start, _p - Start);
        }
    }
}