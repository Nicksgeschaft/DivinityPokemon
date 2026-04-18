using UnityEngine;
using UnityEngine.UI;

namespace PokemonAdventure.UI
{
    // Wires the three QuickMenu buttons to placeholder methods.
    // Replace Debug.Log bodies when Inventory, Map and MainMenu systems are ready.
    public class QuickMenuUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _inventoryButton;
        [SerializeField] private Button _mapButton;
        [SerializeField] private Button _mainMenuButton;

        private void Awake()
        {
            _inventoryButton?.onClick.AddListener(OpenInventory);
            _mapButton?.onClick.AddListener(OpenMap);
            _mainMenuButton?.onClick.AddListener(OpenMainMenu);
        }

        public void OpenInventory()  => Debug.Log("[QuickMenuUI] OpenInventory — placeholder");
        public void OpenMap()        => Debug.Log("[QuickMenuUI] OpenMap — placeholder");
        public void OpenMainMenu()   => Debug.Log("[QuickMenuUI] OpenMainMenu — placeholder");
    }
}
