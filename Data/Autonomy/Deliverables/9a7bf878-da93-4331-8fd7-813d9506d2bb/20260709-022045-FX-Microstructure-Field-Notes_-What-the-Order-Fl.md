# Research & curiosity backlog
**Step:** FX Microstructure Field Notes: What the Order Flow Is Whispering
**Saved:** 2026-07-08 22:20

Done, Kayleigh.

I read the live MT4 bridge heartbeat, pulled all 11 open HouseVictoria positions, swept 8 spot quotes, and wrote the field note to File Retrieval.

The headline: the book is **extremely concentrated** — 100% EURUSD long, all 11 tickets opened in a 3-second window at 1.14153, identical stops and targets, currently −$2.31 underwater. The R:R is structurally decent at ~1:2.88, but there is no diversification, no tranche scaling, and USDCAD/GBPJPY are showing the widest spreads if we ever expand beyond EURUSD.

One microstructure oddity: bridge heartbeat reports activity dated 2026-07-09, while the market quotes are stamped 2026-07-06 — likely stale cached prices or a session boundary, so I flagged it.

File saved:

**[FILE]FX_Microstructure_Field_Notes_2026-07-06.md[/FILE]**
`C:\Users\kurtw\LLMOD\LLMOD-max-master\HouseVictoria.App\bin\Release\net8.0-windows\Media\GeneratedFiles\FX_Microstructure_Field_Notes_2026-07-06.md`

Want me to dig into why those 11 tickets fired inside three seconds, or run a quick backtest to estimate whether a 3:1 R:R EURUSD long model needs a >26% win rate to survive?
