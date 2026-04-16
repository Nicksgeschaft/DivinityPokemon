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
            // Establish faction before base.Awake() initialises RuntimeState.
            // Ensures SightTrigger can detect this unit even when spawned via AddComponent.
            SetFaction(UnitFaction.Friendly);

            // Apply definition stats before base Awake initialises RuntimeState
            if (_definition != null)
                _stats = _definition.BaseStats;

            base.Awake();
        }
    }
}
