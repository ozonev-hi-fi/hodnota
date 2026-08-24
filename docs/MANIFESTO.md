# Manifesto

This document is the founding vision of the project — the motivation behind it and the first step toward creating a service, or perhaps several services.

It is a historical document: it captures the *why* as it was at the very start, and is not updated as decisions change. For the current state of the design, see [architecture.md](architecture.md); for what's actually planned next, see [roadmap.md](roadmap.md); for the reasoning behind specific choices made along the way, see [decisions/](decisions/).

## Backstory

I love listening to music.
When talking with different people (mostly online, through messengers) about music, the need often comes up to share a particular song, album, playlist, or artist. People are lazy — they often won't search for a specific song or album by name using whatever means are available to them, even when it's already clear they'd definitely like it. A convenient and, importantly, legal way to do this is to share a link to that song, album, etc. on some streaming service. People are already used to this, so they expect a ready-made link from you, not just a title. But different people use different streaming services, and never use certain others. This is inconvenient, because you need to know in advance which services the people you're talking to use, search for the song, album, etc. on each of those services (assuming you even have access to them), copy all the links, and send all of them — which, among other things, clutters up the chat, since it's several links that all look the same (e.g., just a wall of blue text), meaning the message needs extra formatting effort just to make the links easy to distinguish. On top of that, it has to be a single message, because a bunch of separate messages — each with its own link preview — looks very unappealing.

Solutions I've seen before:

1. **Songwhip** — a service with a search bar, like Google Search, where you could type the name of an artist, band, song, etc., or paste a link to one of them from a single streaming service, and Songwhip would generate a page collecting links to many different streaming services.
   This service is no longer available. It requires authentication but does not allow registration.
2. **Odesli** — works similarly to Songwhip, but also splits things into sections — streaming, purchase (files or physical media) with links to Bandcamp, Amazon, and so on — and lets you keep a personal collection of such pages. It requires authentication but does allow registration. It has an app for Apple devices that makes creating pages a bit easier: in a streaming service you can tap "share" and immediately pick the Odesli app among the suggested apps, which automatically creates a page. It also has search for podcasts and some other things I never really paid attention to.
   The main problem with this service is stability (it works worse every day) — search used to work poorly, and now it doesn't work at all, neither by keyword nor by a link from one of the supported streaming services. Also, even when the service does recognize the search subject, the resulting page often lacks links to streaming services where the song or album definitely exists. This can be fixed by editing the page and adding your own links, but that's inconvenient. There's also a set of mandatory services — meaning if I want to share a page full of links but don't want a link to yandex music on it (for obvious reasons: no support whatsoever for the terrorist state and its businesses is acceptable), I can't exclude it from the list because it's mandatory. Support for Qobuz (my priority service) is only nominal — Odesli knows about it and recognizes links to it, but doesn't perform automatic search on it, and doesn't automatically add matching songs/albums to new pages even when they exist on Qobuz.

As a developer, I know the principle that when you need to solve a problem, you should first look for an existing solution and only build your own if none exists. So other services probably exist too (like feature.fm, lynkify), but I'm a .NET developer who wants a pet project that lets me practice different technologies in my profession — so why not combine learning with something I'm genuinely interested in.

## Goals

With this project I have two goals: to build a convenient and reliable service that, unlike the aforementioned Songwhip and Odesli, actually does its job well; and also (if not the main goal) to use it in practice to refresh everything I've forgotten and learn everything I don't yet know about full-stack development — from idea to a working, maintained project.

I intend to use various AI services as supporting tools that should automate routine work and help me structure my process so that I actually learn rather than "vibe-code," and I also want (alongside other technologies) using AI to be one of the things I'm learning — that is, to master Claude Code, Antigravity, Copilot, and similar tools purely as tools of a modern developer, not as a replacement for myself, because (for better or worse) that's the world we live in now.

## Authentication and Authorization

Email and password, Google and Facebook authentication, and possibly, at some point later, authentication via an Apple account.

## Rough Shape

Initial architectural thinking is [architecture.md](architecture.md), which is kept up to date as the design evolves. At the time this manifesto was written, the rough shape was: a .NET API, a React web UI, and a MAUI mobile app whose main purpose is to make sharing music from a phone's share sheet effortless, plus room for satellite services (e.g. a Telegram bot) and free/scalable hosting.

## User Experience

The user will have their own profile and a page listing their previously created (or reused) sharing pages. Overall, the service will also have screens for creating, editing, and viewing these sharing pages, plus a home screen with a search bar.

I'd like to have multiple UI/UX themes — dark, light, a classic MS-DOS-style theme, and possibly others — as well as localization support at every level (API, UI, mobile app).

## Data Catalog

The plan is to maintain my own "catalog" of artists, albums, songs, etc., for possible future use in other projects (a kind of Music Wikipedia). Data collection should happen during, and/or in parallel with, the search needed to build the link-collection pages.

## Future Expansion

While building the service, it's important to always keep in mind that it may (and, if the first releases succeed, definitely should) evolve into a service where people share not just music, but other interests as well — movies, artwork, books, or really anything of that kind.

## Supported Streaming Services (First Release, in Priority Order)

- YouTube + YouTube Music
- Qobuz
- Tidal
- Deezer
- Apple Music

### Music Sales Platform Support

- Bandcamp

If the service is well received, these lists should expand later.

## Action Plan

The original plan [roadmap.md](roadmap.md), which is checked off as things get done.
