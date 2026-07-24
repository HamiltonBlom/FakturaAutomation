# Case study — Fakturaautomation

**AI läser fakturan. Koden kontrollerar summorna. Du godkänner. Inget skickas automatiskt.**

*AI-verktyg byggt av HFB Media*

---

## Vad det är

Ett verktyg som automatiserar det tråkigaste jobbet på kontoret: att mata in leverantörsfakturor för hand. Ett AI-lager (Claude) läser fakturan och plockar ut strukturerad data — leverantör, orgnr, radposter, summa, förfallodatum. Sedan kontrollerar C#-koden att siffrorna faktiskt går ihop. Till slut godkänner en människa.

Det här är inte en påhittad hemsida — det är precis den sortens AI-lösning jag bygger åt lokala företag.

## Idén

Fakturainmatning är repetitivt, tidskrävande och lätt att göra fel i. Samtidigt är det inte något man bara kan lämna åt en AI och blunda — betalar man fel belopp till fel konto blir det dyrt.

Så jag byggde det på det enda sätt jag tycker är försvarbart: **låt AI:n göra det den är bra på (läsa och tolka), låt koden göra det den är bra på (räkna och kontrollera), och låt en människa ta det sista beslutet.** AI:n får aldrig godkänna något själv.

## Vad jag byggde

- **AI-extraktion** — Claude läser fakturan och returnerar strukturerad data. Både ren text och PDF funkar; PDF:en skickas rå till modellen som läser den som en människa skulle, inte via opålitlig textutvinning.
- **Kontroll i C#** — koden räknar om summan, kollar organisationsnummer och datumordning. Går något inte ihop blockeras godkännandet.
- **Konsolapp** — fil in, JSON ut. För den som vill köra det i ett flöde eller skript.
- **Granskningsvy (webb)** — en tvåkolumnsvy där fälten står till vänster och radposterna till höger, allt redigerbart. Ändrar du ett belopp körs kontrollen om direkt, och en banner högst upp säger antingen "Redo att godkänna" eller "Kräver granskning: N avvikelser".
- **Godkänn-knapp** — aktiv först när alla avvikelser är lösta. Inget lämnar systemet innan en människa tryckt på den.
- **Signaturdetalj** — fält som AI:n eller kontrollen flaggar får en bärnstensfärgad marginalflagga, en nick till hur en revisor annoterar en pappersfaktura för hand.

## Tekniken, utan trötta ord

- **.NET 8 / C#** i botten — samma sorts stack som riktiga ekonomisystem bygger på.
- **Claude via Anthropic API** med tvingat verktygsanrop, så svaret alltid har rätt struktur.
- **temperature 0** — extraktion är en strukturerad uppgift, inte kreativt skrivande. Samma faktura ska tolkas likadant varje gång.
- **Delad kärna** — extraktions- och kontrollogiken är skriven en gång och används av både konsolappen och webbvyn. Ingen dubbel kod som kan glida isär.
- **Hårda och mjuka signaler hålls isär.** Bara kodens egna kontroller (summa, orgnr, datum) blockerar godkännande. AI:ns egna funderingar visas för granskaren men stoppar inte — för ibland flaggar modellen något som visar sig stämma.
- **API-nyckeln hårdkodas aldrig och loggas aldrig.**

## Vad det visar

Det här är den svåra biten med AI för småföretag: att göra den både användbar och att lita på. Verktyget visar exakt hur jag löser det:

- **Sparar tid** på ett jobb ingen vill göra för hand.
- **Går att lita på** — matten dubbelkollas av kod, inte av gissningar.
- **Människan bestämmer** — AI:n föreslår, du godkänner. Inget sker bakom din rygg.
- **Byggt på riktigt** — .NET och en riktig AI-integration, inte en demo som faller isär vid första skarpa fakturan.

## Vad det betyder för dig

Har du en hög fakturor som någon knappar in för hand varje månad — eller någon annan repetitiv pappersprocess — så är det här sortens lösning jag bygger, till ett rejält lågt pris medan jag bygger upp min portfolio. Din version skulle anpassas efter dina fakturor och ditt flöde.

Vill du att jag visar vad jag menar för just ditt företag?

---

*Kör demon lokalt: kräver .NET 8 och en Anthropic API-nyckel — se `README.md` i mappen. Byggt av Hamilton, HFB Media — hemsidor och enkla AI-lösningar för lokala företag.*
