# Home-Scouting-Bot
A simple bot for doing scouting activities on Discord.

And by simple I mean _simple_. It's also not finished yet and probably (hopefully) won't ever become anything serious.

## Features
The main goal is to group people together and give them a private voice and text channel only they can see without having to resort to cumbersome manual work.  
The secondary goal is to create a Discord bot because I've wanted to do that for a while now.

- Command to setup groups (categories, channels, roles)
- Command to remove groups (categories, channels, roles)
- Command to distribute people currently in a voice channel (with the possibility of excluding a certain role) evenly to arbitrarily sized groups
- Command to break up all groups
- Possibly more, I don't know yet

## Reuse
I think this project is a usable base for other simple bots because [.NET Generic Host](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-5.0) makes it very easy to create a simple but extensible [Discord.Net](https://github.com/discord-net/Discord.Net) application. But (1) this is my first ever interaction with Discord development and (2) the following things were disregarded for this bot:
- Persistent storage with a database
- Customization on guild/server level
- Sharding and other performance related topics (like `IOptionsSnapshot`)
- General multi-guild concerns but it should work fine since it's more or less stateless

It's licensed under MIT and ready for your adaption.
