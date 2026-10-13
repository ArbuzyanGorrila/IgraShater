# CraftMine visual direction

Pixel mine with a clear orthographic grid. The playfield occupies the left two thirds, a narrow expedition panel occupies the right. Source art is Kenney Pixel Platformer, rendered with point filtering; no pre-existing sprites are used. Hearts remain red, copper amber, iron cool gray, gold yellow, diamonds cyan.

Use midnight navy (#111822) as the cave background, slate (#202D3B) for UI surfaces, warm white (#F0E9DC) for text, muted blue-gray (#A7B7C8) for secondary text and amber (#F5BA63) for active controls. UI corners are square. Large readable Russian lettering, distinct button focus and disabled states. Show a directional mining preview and animate each accepted action with one short step/dig beat. No decorative interface motion during puzzle analysis.

Amber marks the primary action or an enabled danger overlay. Secondary buttons use blue-gray surfaces; restarting uses muted red. A warm-white outline marks keyboard focus. Disabled controls retain readable muted captions, skip keyboard navigation, and never dispatch actions. Tab/Shift+Tab cycles buttons; arrows navigate menus spatially and remain movement controls on the board. Only GameUI submits keyboard actions; EventSystem navigation/submit events are disabled.

The fixed 1600×900 composition and board camera use InterfaceLayout's shared letterbox scale and viewport. The gameplay footer keeps objective/trail rules and controls separate from temporary notifications. Ore totals are separate rows so their baselines stay aligned with icons. Settings show the selected volume step and can return to a paused attempt.

Restarting or leaving an unbanked attempt after a move opens a blocking confirmation showing pending XP and ore. Its default focus is “Остаться”; Esc restores the previous screen and focus. The timer and board input stop while it is open. Saved victories remain protected from duplicate banking, and unsaved victories only offer saving again until it succeeds.

Main menu: game title, playable mine vignette and Start / Rooms / Workshop / Sound. Gameplay: room title and counters above the grid, character XP and ore totals in the side panel, contextual instructions beneath. Results protect focus because attempt rewards are committed there. Settings and workshop are full panel views rather than stacked modal cards.
