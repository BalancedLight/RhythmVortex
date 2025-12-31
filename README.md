## Introducing RhythmVortex!

ArrowVortex BPM calculation built into Rhythm Doctor! Also works for the Set Beats Per Minute event! No more tapping or having to boot a separate program!

I must thank my great friend git for doing a lot of this work for me since i don't do a lot of C# coding :D 

### Contains:
* BPM guessing with multiple results for the Play Song and Set Beats Per Minute event
* Set Beats Per Minute calculation has a duration entry to calculate the next given seconds of a song based on the placement of the event.

### Instalation:
Simply drag the given dll into the folder where your mods are held.

Known issues: 
* Deleting a set beats per minute event after a calculation can sometimes cause the editor to freeze? It doesn't like fully crash the game, the keybind to exit the editor (alt-q) still works and you can still use keybinds and such to save.
* On Set Beats Per Minute events, results with a lot of guesses can go under the duration box.
* **BPM detection can take a while on longer songs**
* Cancelling a bpm detection can cause it to be stuck on the "Cancelling..." screen. Reloading the editor fixes, but it's annoying.

To do:
* Add some kind of offset calculation 
* Improve the UI and speed of calculation. 
* Change max and min bpm via preferences (like ArrowVortex, this uses the minimum of 89 BPM and max of 205bpm), hopefully via the pager menu eventually.
* Improve detection
