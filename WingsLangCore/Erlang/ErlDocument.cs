using System.Collections.Generic;
using System.Text;

namespace WingsLang.Core
{
    /// <summary>Un terme Erlang de premier niveau : nœud + point final + trivies.</summary>
    public sealed class ErlTopTerm
    {
        public ErlNode Node;

        /// <summary>Trivie entre la fin du nœud et le point final (whitespace, commentaires).</summary>
        public string BeforeDot = string.Empty;

        /// <summary>Trivie après le point final, jusqu'au terme suivant (ou fin de fichier).</summary>
        public string Trailing = string.Empty;

        /// <summary>Ligne où se termine le terme (ligne du point).</summary>
        public int EndLine = 1;

        /// <summary>Ligne de début du nœud.</summary>
        public int StartLine
        {
            get { return Node == null ? 1 : Node.Line; }
        }
    }

    /// <summary>
    /// Document .lang = entête (commentaires) + suite de termes terminés par des points
    /// (+ trivies). Reprospection exacte tant que rien n'est modifié.
    /// </summary>
    public sealed class ErlDocument
    {
        public string Source;
        public string HeaderTrivia = string.Empty;
        public List<ErlTopTerm> Terms = new List<ErlTopTerm>();

        public string Render()
        {
            var sb = new StringBuilder(HeaderTrivia);
            foreach (ErlTopTerm t in Terms)
            {
                sb.Append(t.Node.Render());
                sb.Append(t.BeforeDot);
                sb.Append('.');
                sb.Append(t.Trailing);
            }
            return sb.ToString();
        }
    }
}