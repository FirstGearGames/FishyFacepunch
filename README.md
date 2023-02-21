# FishyFacepunch

A Facepunch implementation for Fish-Net.

	This is an improved fork from https://github.com/Chykary/FizzyFacepunch and https://github.com/FirstGearGames/FishySteamworks/.

FishyFacepunch brings together **[Steam](https://store.steampowered.com)** and **[Fish-Net](https://github.com/FirstGearGames/FishNet)** . It supports only the new SteamSockets.

## Dependencies
Both of these projects need to be installed and working before you can use this transport.
1. **[Facepunch](https://github.com/Facepunch/Facepunch.Steamworks)** FishyFacepunch relies on Facepunch to communicate with the **[Steamworks API](https://partner.steamgames.com/doc/sdk)**. **Requires .Net 4.x**  
2. **[Fish-Net](https://github.com/FirstGearGames/FishNet)**

## Setting Up

1. Install Fish-Net from the **[official repo](https://github.com/FirstGearGames/FishNet/releases)** or **[Asset Store](https://assetstore.unity.com/packages/tools/network/fish-net-networking-evolved-207815)**.
2. Install FishyFacepunch **[unitypackage](https://github.com/FirstGearGames/FishyFacepunch/releases)** from the release section.
3. In your **"NetworkManager"** object add a  **"Transport Manager"** script and the **"FishyFacepunch"** script.
4. Enter your Steam App ID in the **"FishyFacepunch"** script and set all settings to your needs.

## Host
To be able to have your game working you need to make sure you have Steam running in the background.

## Client
1. Send the game to your buddy.
2. Your buddy needs your **steamID64** to be able to connect. The transport shows your Steam User ID after you have started a server.
3. Place the **steamID64** into **"Client Address"** on the **"FishyFacepunch"** script
5. Then they will be connected to you.

## Sample
I made a sample script **"SteamManager"** to create a dedicated server.

## Testing your game locally
You cant connect to yourself locally while using **FishyFacepunch** since it's using steams P2P. If you want to test your game locally you'll have to use default transport instead of **FishyFacepunch**.
