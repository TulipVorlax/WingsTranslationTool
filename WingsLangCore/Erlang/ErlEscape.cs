using System;
using System.Text;

namespace WingsLang.Core
{
    /// <summary>
    /// Décodage et encodage des littéraux Erlang (chaînes et atomes quotés).
    /// Règles : \n \r \t \b \f \v \s \e \d \a \\ \" \' \^X \NNN(octal) \xHH \x{...}.
    /// </summary>
    public static class ErlEscape
    {
        /// <summary>
        /// Décode le corps d'un littéral string ou atome quoté (sans les guillemets
        /// ou apôtres de délimitation).
        /// </summary>
        public static string Decode(string body)
        {
            var sb = new StringBuilder(body.Length);
            int i = 0;
            while (i < body.Length)
            {
                char c = body[i];
                if (c != '\\')
                {
                    sb.Append(c);
                    i++;
                    continue;
                }
                i++; // saute l'antislash
                if (i >= body.Length)
                {
                    sb.Append('\\');
                    break;
                }
                char e = body[i];
                i++;
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'v': sb.Append('\v'); break;
                    case 's': sb.Append(' '); break;
                    case 'e': sb.Append('\x1b'); break;
                    case 'd': sb.Append('\x7f'); break;
                    case 'a': sb.Append('\x07'); break;
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    case '\'': sb.Append('\''); break;
                    case 'x':
                        if (i < body.Length && body[i] == '{')
                        {
                            int end = body.IndexOf('}', i);
                            if (end < 0) { end = body.Length; }
                            sb.Append((char)HexValue(body.Substring(i + 1, end - i - 1)));
                            i = (end < body.Length ? end + 1 : body.Length);
                        }
                        else
                        {
                            int hex = 0;
                            int taken = 0;
                            while (i < body.Length && taken < 2 && IsHex(body[i]))
                            {
                                hex = hex * 16 + HexDigit(body[i]);
                                i++;
                                taken++;
                            }
                            sb.Append((char)hex);
                        }
                        break;
                    case '^':
                        if (i < body.Length)
                        {
                            char cc = char.ToUpperInvariant(body[i]);
                            sb.Append((char)(cc - (cc == '?' ? (char)0 : (char)64)));
                            i++;
                        }
                        break;
                    default:
                        if (e >= '0' && e <= '7')
                        {
                            int oct = e - '0';
                            int taken = 1;
                            while (i < body.Length && taken < 3 && body[i] >= '0' && body[i] <= '7')
                            {
                                oct = oct * 8 + (body[i] - '0');
                                i++;
                                taken++;
                            }
                            sb.Append((char)oct);
                        }
                        else
                        {
                            sb.Append(e); // échappement littéral peu courant : le caractère lui-même
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Représente une valeur comme littéral string Erlang, avec échappements.
        /// Les caractères Unicode normaux (accents, etc.) restent bruts (UTF-8).
        /// </summary>
        public static string EncodeString(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\v': sb.Append("\\v"); break;
                    case '\0': sb.Append("\\0"); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    default:
                        if (c < 0x20 || c == 0x7f)
                        {
                            sb.Append("\\x");
                            sb.Append(((int)c).ToString("x2"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        public static bool IsPlainAtom(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            if (!(IsAtomStart(name[0])))
            {
                return false;
            }
            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '\'' || c == '#'))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsAtomStart(char c)
        {
            return c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        /// <summary>
        /// Représente un nom d'atome soit en clair, soit sous forme quotée si besoin.
        /// </summary>
        public static string EncodeAtom(string name)
        {
            if (IsPlainAtom(name))
            {
                return name;
            }
            var sb = new StringBuilder(name.Length + 2);
            sb.Append('\'');
            foreach (char c in name)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\'': sb.Append("\\'"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\x");
                            sb.Append(((int)c).ToString("x2"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('\'');
            return sb.ToString();
        }

        private static int HexValue(string s)
        {
            int v = 0;
            foreach (char c in s)
            {
                v = v * 16 + HexDigit(c);
            }
            return v;
        }

        private static bool IsHex(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') { return c - '0'; }
            if (c >= 'a' && c <= 'f') { return c - 'a' + 10; }
            if (c >= 'A' && c <= 'F') { return c - 'A' + 10; }
            return 0;
        }
    }
}