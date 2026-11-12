# Playing Hollowmark on an iPhone

The game now has touch controls. They switch on by themselves on a phone or tablet, so nothing needs setting up
inside the game.

## The touch controls

Hold the phone sideways (landscape).

| Where | What it does |
| --- | --- |
| Left thumb, lower left of the screen | Move. The stick appears wherever you put your thumb. Push past the outer ring to **sprint**. |
| Drag anywhere on the right side | Turn the camera |
| Pinch with two fingers | Zoom the camera in and out |
| **Attack** (big button, bottom right) | Swing your weapon. With a gun it says **Fire**. |
| **Block** | Hold to block. With a gun it says **Aim**. |
| **Roll** / **Jump** | Dodge roll / jump |
| **Use** | Talk, open, buy, sleep, pick up. It lights up when something is in reach. Hold it on the weights. |
| **Reload** | Shows up when you hold a gun. It lights up when the gun is empty. |
| Small round buttons above Attack | Your abilities, once you put them on slots in the Skills screen |
| Top right: **Menu, Bag, Skills, Eat, Drink, Heal** | Pause, inventory, skills, and the quick eat, drink and bandage keys |
| **Close** (top right, in any menu) | Closes the menu |

In menus, tap the buttons as normal. Long lists have wide scrollbars on the right that you can drag.

### Trying it on your Windows PC first

Press **Play** in Unity, then press **F2**. The touch buttons appear and the mouse acts as one finger. For a
real phone screen with notch and multi-touch, open **Window > General > Device Simulator** and pick an iPhone.

## Getting it onto your iPhone

There are two ways. **Way 1 needs no Mac** and is the quickest. **Way 2 makes a real app**, but Apple only
allows iPhone apps to be built on a Mac.

### Way 1: play in Safari (no Mac, free)

1. Close Unity. In **Unity Hub**, go to **Installs**, click the gear next to your Unity version, choose
   **Add modules**, tick **Web Build Support**, and install it.
2. Open the project. In the menu bar choose **Hollowmark > iPhone browser (no Mac) > 1. Set up for phone browser**.
3. Choose **Hollowmark > iPhone browser (no Mac) > 2. Build for phone browser**. The first build takes a while.
   A folder named `Builds/WebGL` opens when it is done.
4. Put it online. The simplest place is **itch.io**, which is free:
   - Make an account, then choose **Upload new project**.
   - Set **Kind of project** to **HTML**.
   - Zip everything *inside* `Builds/WebGL` (select all the files, right-click, **Send to > Compressed (zipped) folder**) and upload the zip.
   - Tick **This file will be played in the browser**, then save. You can leave it as a draft so only you can see it.
5. On your iPhone, sign in to itch.io in Safari, open the game page, tap **Run game**, and turn the phone sideways.
   To make it feel like an app, tap the Share button and choose **Add to Home Screen**.

A browser runs the game slower than a real app, so it may stutter in the bigger dungeons.

### Way 2: a real iPhone app (needs a Mac)

What you need:
- **A Mac** (your own, a friend's, or one at school or a library) with **Xcode** installed. Xcode is free in the Mac App Store.
- **An Apple ID**. The free one you already use on your iPhone works.
- **A cable** to plug the iPhone into the Mac.

On your Windows PC:
1. Close Unity. In **Unity Hub > Installs**, click the gear next to your Unity version, choose **Add modules**,
   tick **iOS Build Support**, and install it.
2. Open the project and choose **Hollowmark > iPhone > 1. Set up for iPhone**.
3. Choose **Hollowmark > iPhone > 2. Build Xcode project**. A folder named `Builds/iOS` opens when it is done.
4. Copy the whole `Builds/iOS` folder to the Mac, for example with a USB stick or OneDrive.

On the Mac:
5. Double-click **Unity-iPhone.xcodeproj** in that folder. Xcode opens it.
6. In Xcode, choose **Xcode > Settings > Accounts**, click **+**, and sign in with your Apple ID.
7. In the left sidebar click **Unity-iPhone** (the blue icon at the top), then the **Signing & Capabilities** tab.
   Tick **Automatically manage signing** and set **Team** to your name (Personal Team).
   If it says the bundle identifier is taken, add a few letters to the end of it, like `com.goodlaith01.hollowmark2`.
8. Plug in the iPhone, unlock it, and tap **Trust** if it asks.
9. On the iPhone, turn on **Settings > Privacy & Security > Developer Mode** and let it restart.
   (This switch only shows up after the iPhone has been plugged into a Mac with Xcode.)
10. At the top of Xcode, pick your iPhone from the device list, then press the **Play** (▶) button.
11. The first time, the iPhone says the developer isn't trusted. Go to
    **Settings > General > VPN & Device Management**, tap your Apple ID, and tap **Trust**. Then open Hollowmark.

With a free Apple ID the app stops opening after **7 days**. Plug the phone into the Mac and press Play in
Xcode again to renew it. A paid Apple Developer account ($99 a year) makes it last a year
and lets you install through TestFlight without a cable.

## Notes

- iOS needs a Mac for the last step of every app build. There is no way around that with a free Apple ID.
  Cloud build services (Unity Build Automation, GitHub Actions on macOS) can do it without your own Mac,
  but they need the paid Apple Developer account and more setup.
- The touch code lives in `Assets/Scripts/UI/TouchControls.cs` and `Assets/Scripts/UI/UiScale.cs`. The menu
  items are in `Assets/Editor/MobileBuild.cs`.
