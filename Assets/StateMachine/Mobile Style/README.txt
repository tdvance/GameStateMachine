# Mobile Style State Machine

## Start

The default start state.  One can do basic initialization here.  This would be attached to the scene from which the game is run, the scene that contains all the Singletons (including the StateMachine singleton).  

### Transitions to:

+ Splash -- immediately, after 0.1 second delay to prevent race conditions 

## Splash

A splash screen, may include brief music or a sound, or just start the title music here.  Here goes a logo for the developer.  

### Transitions to:

+ Title -- 2.5 seconds after splash screen is loaded

## Title

The main title with graphics and title music.

### Transitions to:

+ Options -- if Options button is pressed
+ Progress Map -- if Play button is pressed

## Options

Options menu.  May include settings for difficulty, sound effects volume, music volume, etc.  

The scene for this state should probably be loaded additively, with the scene-in-progress paused, so hitting Done can resume the scene in progress.  It is up to the developer if this would also include pausing or changing the music, or just leaving the music already playing.

### Transitions to:

+ last state in state history (the state that transitioned to this state) when Done button is pressed

## Progress Map

Map with grayed-out level buttons that become un-grayed-out when a level is unlocked.  Map can have multiple pages, and there would be page buttons to move forward or back.   Optionally, buttons to replay unlocked cut scenes.

### Transitions to:
+ Options -- if Options button is pressed
+ Cutscene -- if an unlocked cutscene button is pressed
+ Level -- if a game level button is pressed 
+ Title -- if Quit button is pressed

## Level

The actual game level

### Transitions to:
+ Options -- if Options button is pressed
+ Level Up -- if level is successfully completed
+ Game Over -- if level ends unsuccessfully or player presses Exit button

## Level Up

Unlocks next level (if not already unlocked, such as when a level is being replayed)

### Transitions to:
+ Options -- if Options button is pressed
+ Level -- if Continue (for next level) or Replay (for this level) is pressed
+ Progress Map -- if Progress Map button is pressed
+ Cutscene -- if Continue is pressed and "next level" is really a cutscene

## Game Over

End of level without successful completion

### Transitions to:
+ Options -- if Options button is pressed
+ Level -- if Replay (for this level) is pressed or if Continue is pressed (for next level) and next level is unlocked
+ Progress Map -- if Progress Map button is pressed

## Cutscene

Cutscene that sometimes plays between levels

### Transitions to:
+ Progress Map -- when cutscene is done playing
