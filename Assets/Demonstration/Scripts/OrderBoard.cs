using Sc4ve.Multimodality;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// Le panneau de bons de commande, visible depuis la cuisine (§5 du README) : ce que
    /// chaque table attend, lisible par un spectateur en trois secondes. Non interactif, non
    /// sémantisé — c'est du HUD, pas un objet du monde ; l'annoter ferait répondre
    /// « sélectionne le tableau » et polluerait le graphe.
    ///
    /// **Ce tableau est une projection, jamais une seconde source de vérité.** Il ne détient
    /// aucun état : il relit les CustomerOrder de la scène à chaque rafraîchissement, et c'est
    /// ce sondage — et non un instantané pris au démarrage — qui permet à un client apparu
    /// tard d'avoir sa ligne. Si quelqu'un y ajoute un cache « pour éviter de scanner », le
    /// critère 2 du lot 4 (« reste consultable ») devient « affiche ce qu'on lui a dit un
    /// jour », et la divergence sera indétectable. Aucun membre public, à dessein : le tableau
    /// n'est pilotable par personne.
    ///
    /// Chaque ligne affiche le PLAT PRÉCIS énoncé par le client — le tableau dit la même
    /// chose que la voix, et c'est sa seule fonction. (L'ancien « Salade ? » qui exhibait la
    /// sous-spécification a disparu avec elle : le client nomme désormais un plat.)
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    public class OrderBoard : MonoBehaviour
    {
        // 4 Hz en temps RÉEL : c'est de l'affichage, il ne doit pas se figer au ralenti.
        private const float RefreshInterval = 0.25f;

        private TextMesh _text;
        private float _nextRefresh;

        private void Start()
        {
            _text = GetComponent<TextMesh>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh || _text == null) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;

            _text.text = Compose();
        }

        private string Compose()
        {
            bool french = UserData.Locale == "fr";
            var lines = new StringBuilder(french ? "COMMANDES" : "ORDERS");

            // FindObjectsInactive.Include : un client désactivé (parti, futur niveau) garde sa
            // ligne — le tableau raconte la partie, il ne montre pas seulement le présent.
            //
            // Tri par POSITION DE TABLE (z puis x) : les tables ne sont pas numérotées (§5),
            // l'ordre spatial est la seule identification qui ne trahisse pas la deixis. Vu
            // depuis la cuisine (regard vers +z), la liste se lit dans l'ordre de la salle.
            CustomerOrder[] customers = FindObjectsByType<CustomerOrder>(FindObjectsInactive.Include)
                .Where(c => c.Table != null)
                .OrderBy(c => c.Table.transform.position.z)
                .ThenBy(c => c.Table.transform.position.x)
                .ToArray();

            foreach (CustomerOrder customer in customers)
            {
                string line = customer.State switch
                {
                    // Avant la prise de commande, l'ATTENTE et rien d'autre : dire qu'un
                    // client attend n'apprend rien de ce qu'il veut, alors qu'afficher sa
                    // famille de plats révélerait la commande avant qu'il l'ait énoncée
                    // (critère 1). La ligne existe quand même, sinon le tableau serait vide
                    // au démarrage — et une table sans ligne se lit « personne » plutôt que
                    // « personne n'est encore allé la voir ».
                    CustomerOrder.Stage.Seated => french ? "— en attente —" : "— waiting —",
                    CustomerOrder.Stage.Gone when !customer.HasOrdered
                        => french ? "— parti sans commander —" : "— left without ordering —",
                    _ => Row(customer, french),
                };
                if (line != null) lines.Append('\n').Append(line);
            }

            return lines.ToString();
        }

        private static string Row(CustomerOrder customer, bool french)
        {
            // Le plat PRÉCIS, tel que la voix l'énonce : depuis que le client commande un plat
            // nommé (« Salade César ») et non une famille, « Salade ? — sans banane » racontait
            // une autre histoire que la parole. La contrainte n'est plus affichée non plus —
            // le plat demandé la satisfait déjà, et elle ressurgit au refus, seul moment où
            // elle apprend quelque chose (même règle que BuildSpokenOrder).
            //
            // Le vocabulaire peut n'être pas encore lu : mieux vaut une ligne franche qu'une
            // ligne vide qui passerait pour « pas de commande ».
            string dish = customer.DishLabel
                          ?? customer.FamilyLabel
                          ?? (french ? "(illisible)" : "(unreadable)");

            string mark = customer.State switch
            {
                CustomerOrder.Stage.Served => "  ✔",
                CustomerOrder.Stage.Gone => "  ✘",
                _ => "",
            };

            return $"{dish}{mark}";
        }
    }
}
