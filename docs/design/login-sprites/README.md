# PLANORA login sprites

The animated login composition follows the supplied reference: icy glass panels,
an overlapping blue productivity sculpture, a usable HTML form, and a centered
footer logo. The artwork is reconstructed, so it is not a pixel-exact extraction.

Design assessment: impact 4, context fit 5, feasibility 5, performance 4,
consistency risk 4; DFII 14. Existing branding, copy, and typography are retained
to match the reference.

## Assets

Nine independent transparent PNG sprites live in
`src/SPWPOS.Api/wwwroot/assets/login-sprites/`:

| File                    | Layer                                                 |
| ----------------------- | ----------------------------------------------------- |
| `ribbon.png`            | Back of the sculpture                                 |
| `calendar.png`          | Main calendar board                                   |
| `checklist.png`         | Floating checklist in front of the calendar           |
| `clock.png`             | Foreground clock, including its hands                 |
| `ribbon-front.png`      | Curved ribbon cradle crossing the bottom of the clock |
| `ribbon-background.png` | Faint flowing ribbon behind the overall composition   |
| `workspace-photo.png`   | Circular desk photo and frame                         |
| `bubble.png`            | Reused for independent decorative spheres             |
| `orb-motion.png`        | Unused earlier generated motion-orb variant           |

Lossless WebP copies provide smaller runtime downloads. Their visible pixels and
alpha were verified against the PNGs. PNGs remain available as source assets and
fallbacks. The built-in image generation tool produced each sprite in a separate
call; the original prompt set is saved in `prompts.json`, and the foreground and
background ribbon additions are documented in `depth-prompts.json`. The motion
orb prompt and its material reference are saved in `atmosphere-prompts.json`.

The clock was regenerated with exactly twelve hour markers. Its correction prompt
is saved in `clock-correction-prompt.json`; the PNG retains the original canvas
size and transparency, and its lossless WebP copy matches the visible pixels.

### Calendar and checklist frame variants

Each has three transparent frames: the unchanged original plus two generated
edits. The calendar variants change appointment cell fills and dots. The checklist
variants show one and two completed items; the original shows all three.
`frame-prompts.json` records the built-in image generator's edit prompts.

| Sprite    | Original        | Variant 2                | Variant 3                |
| --------- | --------------- | ------------------------ | ------------------------ |
| Calendar  | `calendar.png`  | `calendar-frame-02.png`  | `calendar-frame-03.png`  |
| Checklist | `checklist.png` | `checklist-frame-02.png` | `checklist-frame-03.png` |

PNG and lossless WebP versions share the original canvas dimensions. Frames
stack inside their existing sprite wrapper so position, bobbing, rotation and
shadows stay shared. A 12-second cycle crossfades through the three states with
roughly one-second transitions; the checklist starts three seconds later. Frame
cycling starts after the entrance completes, pauses with other motion, and uses
the original frame for reduced motion. The original stays opaque below incoming
frames to avoid dimming the shared white surfaces during a fade.

## Composition and animation

All authentication steps share this animated composition: login, registration,
email verification, password recovery, recovery OTP and password reset. Only the
form changes between steps. The artwork and motion control remain mounted, so
the entrance does not restart and the manual pause setting survives navigation.
Forms remount per step to clear passwords, OTPs and validation feedback.

Panel changes use a 140ms blurred slide out, followed by a 460ms glass-card
height transition and staggered field reveals. Returning to login reverses the
slide direction; a soft light sweeps across the glass during the change. The
artwork remains live. Departing controls become inert, and a newer navigation
cancels pending work to prevent stale forms appearing. Reduced motion or manual
pause switches panels immediately. Scroll anchoring is disabled for this layout
to prevent viewport jumps as the card grows.

The five main sculpture layers, from back to front, are rear ribbon → calendar →
checklist → clock → foreground ribbon cradle. Their wrappers expose `data-depth`
values 1–5 and use CSS stacking levels 10–50. The headline panel sits between the
calendar and checklist. The photo and spheres are independent decorative accents.
The faint background ribbon is a separate page-level sprite behind the sculpture
and form; it is not part of the five-layer stack.

The large background ribbon spans 148% of the desktop scene at 20% opacity.
The same pale ribbon is reused at a smaller scale over the bottom-right corner
of the login card. Three larger motion-orb wrappers reuse the original `bubble`
sprite near the page borders. An SVG Gaussian filter (`stdDeviation="14 1.5"`)
applies directional blur at different angles while keeping the sphere's original
lighting upright. The generated `orb-motion` variant is no longer used. These
decorations ignore pointer events and stay away from the form text and controls.

The active rear and foreground ribbon layers both use `ribbon.png` / `ribbon.webp`
with shared geometry variables. A clip path reveals its lower front sweep above
the clock, so colors and edges align exactly and the ribbon appears continuous.
`ribbon-front.png` remains available as the earlier standalone cradle variant.
The two active ribbon layers have identical bobbing phase, amplitude, rotation
and deformation, keeping their pixels aligned throughout.

A cyan-white shine sweeps along the main ribbon's edges every eight seconds.
An SVG mask subtracts an eroded copy of the sprite alpha to isolate its rim.
Both ribbon layers share the same moving highlight, so the front clip and sprite
stack preserve the sculpture's depth. The sweep starts after the entrance,
pauses with the other motion, and is hidden for reduced motion.

The headline panel uses a CSS perspective transform that makes the right edge
recede and warps the HTML lettering with the glass. At the desktop reference size,
the right edge is approximately two-thirds the height of the left edge. Mobile
uses a gentler angle. Visible blue contact and ambient shadows separate the
overlapping sprites; the ground shadow uses the same blue palette. These are
static depth treatments.

`frontend/src/login-scene.jsx` defines each independent `.login-sprite` wrapper
and its `data-sprite` name. The percentage positions, scale, and depth are in
`frontend/src/styles/login-scene.css`. Headline lines, the glass headline panel,
form card, and background shapes are separate DOM/CSS elements.

Native CSS animations use a closed 9-second bobbing cycle with smooth turning
points, reduced vertical travel, gentle rotation and different phase offsets for calendar, checklist, clock,
photo and small orbs. The calendar remains nudged 20px left. The two main ribbon
halves share a phase so the ribbon stays continuous. The faint background ribbon
has a slower cycle. Edge orbs oscillate along their blur axis over 11 seconds,
keeping fixed opacity after the initial reveal. On each login-page mount, the
logo and headline fade in first; the login card starts its fade after 600ms.
Artwork reveals after its images decode. The sculpture has an additional 300ms
delay, preserving the ribbon → calendar → checklist → clock → photo order.
A staggered
1600ms scale animation shrinks the complete foreground composition from 108%
to its resting size as one composited layer, with an 8px blur clearing to zero
over the same circular easing curve. The blur is removed at the end so the
resting composition is sharp. The sculpture, headline panel, logo
and login card retain their relative positions and overlaps. Foreground children
have staggered opacity fades and ongoing floating motion, with no individual
entrance scaling. The faint background ribbon, distant orbs and glass shapes
expand individually from 82–84%, including the halo behind the headline. The
opposing scale directions create a dolly-zoom-style depth effect. Mobile scale
travel is halved. Foreground and background scaling share an ease-in-out circular
curve; the opacity fade has its own timing. Both main ribbon layers reveal together.
Entrance opacity and scale are independent of the floating motion, so finishing
the reveal does not reset its phase. Form interaction or pausing completes the
reveal immediately; reduced motion displays the whole composition immediately.
The ribbons gently flex using a closed stretch-and-shear cycle. The two main
halves share a 16-second deformation; background and card ribbons use slower,
offset cycles. Transforming the artwork preserves the existing blue shadows.
The headline and form stay stationary. A light ray sweeps across both blue lines
of “phù hợp với bạn” for 1.2 seconds every five seconds. Decorative, hidden-from-
accessibility text overlays carry the highlight; the solid blue copy stays visible.
Mobile motion amplitudes are halved
without restarting the animation phase on resize.

With a fine pointer and hover support, the calendar, checklist and clock grow to
104.5% over 320ms on hover and smoothly return when the pointer leaves. Scaling
the sprite wrapper keeps crossfading frames aligned and composes with floating
motion. Reduced-motion preferences disable this hover effect.

The footer provides a pause/resume button. Motion also pauses on form interaction
and while the document is hidden. Changing the system reduced-motion preference
reverts animations to the original static geometry; leaving the login page
cleans up the motion state and event listeners. Translation, rotation, ribbon
deformation and the light sweep all pause together. Reduced motion also disables
the light sweep. The directional blur and blue shadows remain fixed. Clock hands and individual
checkmarks are baked into their sprites, so internal movement needs more layers.

The original motion research included [Material choreography](https://m1.material.io/motion/choreography.html),
[Material easing](https://m1.material.io/motion/duration-easing.html), and
[Apple motion guidance](https://developer.apple.com/design/human-interface-guidelines/motion).
The current implementation uses native browser animations with independent
phases, a stable focal point, and optional motion.

At 800px and below the scene stacks above the form. On short mobile landscape
screens the illustration is hidden to keep the form accessible. Account creation
and recovery retain their existing shared layout and illustration.
