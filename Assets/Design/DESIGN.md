# Game Design

This document outlines the visual and interaction design specifications for the project.

## Crafting UI

**Brief summary:** A "Holographic Industrial" aesthetic. It blends heavy, scavenged metal parts with high-tech projected interfaces. The goal is to make items feel like precious discoveries being analyzed and assembled in a high-tech scavenger's workbench.

### UI Design

- **Color system:** 
    - **Base Surface:** Deep Void (`#050507`) — textured with subtle metallic scratches.
    - **Physical Panels:** Scavenged Iron (`#121214`) — heavy, bolted-down containers.
    - **Holographic Primary:** Plasma Cyan (`#00F2FF`) — high-intensity glow for text and active projections.
    - **Holographic Secondary:** Binary Teal (`#008B8B`) — lower intensity for background grids and secondary data.
    - **Accent/Energy:** Overheat Amber (`#FFB300`) — used for "CRAFT" and critical energy states.
    - **Success:** Biosync Green (`#39FF14`) — for completed crafting and valid material counts.
- **Typography:** 
    - **Display/Headline:** Bold, Condensed Monospace (32pt+) — feels like a digital readout.
    - **Body:** Clean Sans-Serif (15pt) — high legibility for lore and descriptions.
    - **Data Labels:** Tiny Monospace (9pt, Cyan) — "SERIAL NO.", "MASS", "ENERGY REQ".
- **Layout:** 
    - **Observation Deck (Center):** A large, dedicated focus area for the selected item. It uses a circular "Scanning Grid" base and floating holographic brackets to frame the item.
    - **Floating Data Layers:** Information (Recipe List and Requirements) is projected on semi-transparent "Glass" panels that tilt slightly in 3D space to create depth.
    - **Material Slots:** Circular high-contrast slots. When empty, they show a "Missing" ghost icon in amber. When filled, they glow in sync green.
- **Components:** 
    - **Item Slots (core):** Dark glass base with inner cyan glow. Borderless, using light to define edges.
    - **Interactive Brackets (core):** Corner-only holographic frames that "snap" onto a selected item.
    - **Craft Button (core):** An "Energy Core" look. Pulsing amber gradient with a thick, glowing outer ring.

### Asset Design

- **Visual identity:** "High-Tech Scavenger" — mixing rusted valves and heavy bolts with glowing fiber-optics and glass projections.
- **Palette:** Dominant: Near-Black Iron; Secondary: Cyan Glow; Accent: Amber/Green.
- **Composition rules:**
    - **Contrast is King:** Items are always placed against the darkest base surface to make their colors/silhouettes pop.
    - **Depth Layering:** Physical elements (metal plates) sit at the back; Holographic elements (grids, text) float 5-10px in front.
- **Reference:** Match the texture detail of `Scrap_MetalPipe` for physical parts, but overlay them with a clean `VFX_Hologram_Scanline` shader effect.

### Game Feedback

- **Genre profile:** High-Energy / Tactical Hybrid. Immediate, "electrifying" response to every action.
- **Interaction map:**

| Interaction | Tier | Importance | Camera | Time | Transform | Visual | Audio | Input | Rationale |
|------------|------|-----------|--------|------|-----------|--------|-------|-------|-----------|
| Hover Item | core | minor | — | — | Pop (1.05x) | Scanline flash | Low Hum | — | Highlights clarity and selection. |
| Select Item | core | light | Zoom (subtle) | — | Snap-to-center | Focus brackets | Digital Chirp | — | Anchors focus on the selected item. |
| Crafting Start | core | medium | Shake (constant) | Slow-mo (0.9x) | — | Energy vortex | Surge/Buzz | Rumble (low) | Builds tension during assembly. |
| Crafting Finish | core | heavy | Impulse | Freeze (0.05s) | Bloom burst | Light beam | "Success" Chord | Rumble (peak) | The 'Wow' moment of creation. |

- **Assets needed:**
    - **Scanning Grid (core):** A circular, pulsing floor texture with data nodes.
    - **Holographic Brackets (core):** 4 L-shaped corner sprites with an additive glow.
    - **Data Particles (optional):** Bits of "code" or "data" floating upward from the item.
    - **Glass Panel (core):** Semi-transparent dark blue panel with a light cyan rim.

- **Sequences:**
    - **Selection Flow:** User clicks recipe (0ms) -> Item moves to Observation Deck (50ms, ease-out) -> Brackets snap to corners (100ms, spring) -> Focus lights turn on (150ms).
    - **Crafting Explosion:** Gauge hits 100% (0ms) -> Screen turns white for 2 frames (0ms) -> Item materializes from particles (30ms) -> Radial blur expands and fades (200ms).
