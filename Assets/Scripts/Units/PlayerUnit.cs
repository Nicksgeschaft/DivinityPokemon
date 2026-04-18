using PokemonAdventure.Data;
using PokemonAdventure.ScriptableObjects;
using UnityEngine;

namespace PokemonAdventure.Units
{
    // ==========================================================================
    // Player Unit
    // Concrete BaseUnit for player-controlled Pokémon characters.
    // Reads its base stats from a PokemonDefinition ScriptableObject.
    // ==========================================================================

    public class PlayerUnit : BaseUnit
    {
        [Header("Pokemon Definition")]
        [Tooltip("Assign the matching PokemonDefinition asset for this character.")]
        [SerializeField] private PokemonDefinition _definition;

        public PokemonDefinition Definition => _definition;

        protected override void Awake()
        {
            SetFaction(UnitFaction.Friendly);

            if (_definition != null)
                ApplyDefinition(_definition);

            base.Awake();
        }

        // Called after AddComponent when the definition is assigned at runtime
        // (e.g. from DebugSceneSetup). Re-initialises RuntimeState with correct stats.
        public void Initialize(PokemonDefinition definition)
        {
            _definition = definition;
            if (_definition == null) return;
            ApplyDefinition(_definition);
            RuntimeState.Initialize(_stats);
        }

        private void ApplyDefinition(PokemonDefinition definition)
        {
            _stats = definition.BaseStats;
            SetDisplayName(definition.PokemonName);
        }
    }
}
