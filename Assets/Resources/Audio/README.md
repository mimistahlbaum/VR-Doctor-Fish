## Audio Assets

The project uses one background music track and five sound-effect files.
They live under `Assets/Resources` so `AudioController`
(`Assets/Scripts/DoctorFish/AudioController.cs`) can load them by name at
runtime and they are always included in builds.

### Background Music

`drfish.mp3` is the main background music for the VR experience. It should begin when the experience starts and continue throughout the session. The track may be looped if necessary and should fade out when the experience ends.

### Small Fish Sound Effects

`drfish_fish_se1.mp3` and `drfish_fish_se2.mp3` are two variations of the small fish nibbling sound. They should be played as one-shot sound effects when small fish approach or nibble the user’s feet. The two files may be selected randomly to avoid repetition.

### Large Fish Sound Effect

`drfish_big_se1.mp3` is used for the large fish interaction. It should be played once when the large fish approaches or bites the user’s foot.

### Jellyfish Sound Effect

`drfish_jelly_se1.mp3` represents the jellyfish sting or electrical sensation. It should be played as a short one-shot sound when a jellyfish touches the user’s leg.

### Water Entry Sound Effect

`drfish_pop_se1.mp3` is a soft water pop played once at the start of the welcome stage, when the feet enter the water.

### Sound credit

- Sound effects: CC0, sourced via Pixabay and edited pitch via Audacity
- Background music: Sound motifs were generated via ACE Music and Gemini, edited and composed via GarageBand
