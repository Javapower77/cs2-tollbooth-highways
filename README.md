# Tollbooth Highways for Cities Skylines II

This project is part of my journey of learning to make mods in this amazing game that I had loved for years.
Is based on Unity ECS and DOTS.
The construction of this mod is, of course, thanks to the amazing collobartion of some many people on CS2 Mod Discord server who are very nice, patiente and with high of human quality.
To all of them, I am eternaly gratful to be patience with me and help in every question I had mad.
This mod of course will evolute meanwhile I keep learning new stuf in this type of developing.

## Explore the Documentation

* [CS:2 System Order Phases/Systems](/TollboothHighways/Docs/SystemOrder.md)
* [How manual tolbooth works](/TollboothHighways/Docs/StopVehiclesOnRoadSystem.md)
* [Diagram for the manual tollbooth road](/TollboothHighways/Docs/Tollbooth-Barrier-Flow.md)
* [Images previews from the mod](/TollboothHighways/Docs/ModsPreviews.md)
* [How logging in IJob with Burst Compiled code](/TollboothHighways/Docs/JobLogger.md)

## Objetive

* Have a manual tollbooth with barrier that makes vehicle stop and emulate the behaivor of a real word manual payment.
* Have an automatic tollbooth where vehicle slow down the speed and go trough the tollbooth and the payment is automatic.
* Have five different type of tollbooth road (with manual and automatic payment).
  * Public transport only (Buses and Taxis)
  * Private transport (Private Car and Motorcycle)
  * Heavy transport (Delivery truck only)
  * Service transport (all kind of service transit)
  * All transport (allow any king of vehicle car go trough)
* By using the Mod Setting it has a lot of configuration to set the price according the type of vehicle, the time of the day and the weekends.
* Clicking on the toll booth the game will show the type of vehicles, the quantity and the aumont of many generated so far.
* A main Panel will represent all toll booth generating the money the will inject to the main game.
* Ability to associate the toll road to a specific districts

Well, that is the goal of this mod and that is what I am going to try to achieve. Is most likely that CO realease some Toll DLC sooner, but that will not stop me to continue on this mod, because as I had metinoned, it is all about learning. 🇦🇷 💻 🤟

## Important

First you must create the road highways and the add the tollbooth roads because
the other way it will remove the CarLaneFlags to indicate what type of vehicles
could passthrough the tollbooth lane. I did'nt found any method to avoid this.

## Note

As CS:2 doesn't have any exposed method to control what vehicle go through a lane of a road, this mod just try to do the best and sometimes cars will not respect excluise tollbooth road. In a future, is CO add flags for restrict type of vehicles I will modify this mod to use as much vanilla methods as possible.

## Thanks

* krzychu124
* bruceyboy24804
* klyte45
* StarQ / Qoushik
* yenyang
* Nullpinter
* Konsi (Mimonsi)

## A very super hyper Alpha version 😆

![Preview Automatic Tollboths](/TollboothHighways/Docs/Images/Example1.jpg)
