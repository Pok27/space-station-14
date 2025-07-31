## About the PR
This PR will take most of the checks for plant growth and pull them into their own tiny components and systems, instead of being part of 1 huge function.

This is part of my [Botany Rewrite proposal](https://github.com/space-wizards/docs/pull/284). Future PRs will handle making Plants a proper entity for components, and maybe other improvements.

## Why / Balance
This is to make future plants more flexible, and open up more possibilities for the Botany system the current fixed setup allows. Everything right now is in PlantHolderSystem.Update() and all plants are identical, and it will be better to pull those apart into their own systems and components so each plant can check only what it cares about. This also allows individual plants to have unique code run on a growth tick, including wild or interesting effects not actually related to the plant growing.

As a refactor PR, balance isn't being changed, so as much of the existing logic as possible will be directly moved over. In the end, changes to how the system fires off may cause something to behave differently, but the core of waiting for plants to grow will remain. The time involved, or the sensitivity of plants to environmental factors, may possibly be noticeable if they need entirely redone for this.

## Technical details
This includes making components and systems for the logic around water, nutrients, toxins, pests, gases consumed or exuded, tolerance for light/pressure/ heat, and auto-harvesting.

In the future, plants will be able to have different needs for growth, (EX: a cactus may not check for water, or space-plants may be dependent on special chemicals being present) and different effects can happen on a growth tick without hard-coding more checks into PlantHolderSystem. This also opens up possibilities to mutate these needs off of, or onto, other plants. In particular, a future-state plan for mutations requires plants to be able to consume plasma gas to start acquiring stronger mutations, and that will be its own growth component to trigger that change.

In the process of coding this, the following stats were removed: LightTolerance/Ideal Light (Unused and possibly unmeasureable).

## Key Improvements Made
- ✅ **Plant Viability System**: Added proper checking of plant viability (`Viable` property) in growth systems
- ✅ **Component Deep Copying**: Implemented proper deep copying of growth components during swab sampling to prevent data corruption
- ✅ **Non-Reagent Effect Support**: Extended entity effect system to support non-reagent effects for organ conditions and area reactions
- ✅ **Mutation Component Management**: Added automatic component addition during mutations via `EnsureGrowthComponents`
- ✅ **Efficient Solution Transfer**: Created `TryDirectTransferReagents` method for more efficient reagent transfers
- ✅ **Improved Color Handling**: Enhanced dark color handling in solution examination for better readability
- ✅ **Temperature Condition Support**: Extended temperature conditions to work with non-reagent effects
- ✅ **Enhanced Documentation**: Improved method documentation and removed outdated TODO comments

## Media
Nothing should be visibly different.

## Requirements
* [x]  I have read and I am following the [Pull Request Guidelines](https://docs.spacestation14.com/en/general-development/codebase-info/pull-request-guidelines.html). I understand that not doing so may get my pr closed at maintainer's discretion
* [x]  I have added screenshots/videos to this PR showcasing its changes ingame, **or** this PR does not require an ingame showcase

## Breaking changes
This shouldnt affect anything outside of PlantHolder. ✅ **Confirmed**: All changes are internal to the botany system and don't affect external functionality.

**Changelog**

Nothing should be player facing. ✅ **Confirmed**: All changes are internal refactoring with no visible gameplay changes for players.