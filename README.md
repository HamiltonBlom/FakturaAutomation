# Fakturaautomation – Core, konsolapp och Blazor-granskningsvy

Automatiserar inmatning av svenska leverantörsfakturor. Ett AI-lager
(Claude, Anthropic Messages API med tvingat verktygsanrop) extraherar
strukturerad data ur fakturatext; C# validerar summorna. Repot innehåller
tre projekt:

```
FakturaExtraktion.Core/    Delad backend: modeller, extraktion, validering
FakturaExtraktion/         Konsolapp (fil in -> JSON ut)
FakturaExtraktion.Web/     Blazor Server-app: interaktiv granskningsvy
```

`FakturaExtraktion.Core` innehåller ingen egen app – bara de typer och
tjänster som både konsolappen och webb-UI:t använder. Ingen
extraktions- eller valideringslogik är skriven två gånger.

## Förutsättningar

- .NET 8 SDK
- En Anthropic API-nyckel

## Sätt API-nyckeln

Samma nyckel används av båda apparna (var och en läser sin egen
konfiguration/user-secrets, men miljövariabeln fungerar för båda):

```bash
# macOS/Linux
export ANTHROPIC_API_KEY="din-nyckel"

# Windows (PowerShell)
$env:ANTHROPIC_API_KEY = "din-nyckel"
```

Alternativt, per projekt:

```bash
cd FakturaExtraktion.Web    # eller FakturaExtraktion
dotnet user-secrets set "ANTHROPIC_API_KEY" "din-nyckel"
```

Nyckeln hårdkodas aldrig och loggas aldrig.

## Bygg allt

```bash
dotnet new sln -n FakturaExtraktion
dotnet sln add FakturaExtraktion.Core/FakturaExtraktion.Core.csproj
dotnet sln add FakturaExtraktion/FakturaExtraktion.csproj
dotnet sln add FakturaExtraktion.Web/FakturaExtraktion.Web.csproj

dotnet restore
dotnet build
```

Sandboxen där koden skrevs saknar nätverksåtkomst till nuget.org, så jag
har inte kunnat köra `dotnet build` för att verifiera kompileringen där.
Koden är läst igenom noggrant för kända fallgropar (nullable-varningar,
Razor-parsning, paketberoenden), men kör `dotnet restore && dotnet build`
som första steg lokalt. Om restore klagar på en specifik paketversion,
kör t.ex. `dotnet add package Microsoft.Extensions.Http.Resilience` i det
berörda projektet för att hämta senaste kompatibla version.

## Kör konsolappen

```bash
cd FakturaExtraktion
dotnet run -- exempel/exempel-faktura.txt
```

Exitkod: 0 = klar, 2 = granskning krävs, 1 = fel. Se kommentarer i
`Program.cs` för detaljer.

## Kör Blazor-granskningsvyn

```bash
cd FakturaExtraktion.Web
dotnet run
```

Öppna adressen `dotnet run` skriver ut (t.ex. `https://localhost:7xxx`).
Sidan visar:

1. **Val av faktura** – en dropdown med två inbyggda exempel (en komplett
   faktura, och en med ett äkta summeringsfel) eller uppladdning av en
   egen `.txt`-fil. Klicka **Kör extraktion**.
2. **Banner** högst upp – grön "Redo att godkänna" eller gul
   "Kräver granskning: N avvikelser".
3. **Granskningsvy** i två kolumner – fält till vänster (redigerbara),
   radposter + summa till höger (redigerbar tabell).
4. Ändrar du ett belopp körs valideringen om direkt (samma
   `IInvoiceValidator` som konsolappen använder) och banner/markeringar
   uppdateras utan sidladdning.
5. **Godkänn faktura** – aktiv först när inga avvikelser kvarstår. Skriver
   den godkända fakturan som JSON till loggen (stub, ingen riktig
   ekonomisystem-integration) och visar en bekräftelse.

## Antaganden – Blazor-UI:t

1. **Blazor Server (Interactive Server, global rendermode) valdes**
   framför WebAssembly eller en separat Web API + SPA-lösning: all C#-kod
   (inklusive Anthropic-anropet, som kräver en hemlig API-nyckel) körs
   redan server-side, så det behövs ingen extra API-yta för att UI:t ska
   kunna anropa `IInvoiceExtractor`/`IInvoiceValidator` direkt via DI. Det
   ger också den "leva omvalidering"-känslan uppdraget efterfrågar utan
   egen JavaScript.
2. **Rent Razor + CSS, inget UI-bibliotek** (t.ex. MudBlazor). Dels för att
   undvika ytterligare ett externt paket vars exakta version jag inte kan
   verifiera i den här sandboxen, dels för att kraven (två kolumner,
   tabell, banner, färgmarkering) är enkla att uttrycka i vanlig CSS.
3. **`IFakturaService`** (i `FakturaExtraktion.Web/Services`) är ett nytt,
   litet interface enligt uppdragets instruktion – backend hade inget
   samlat "extrahera + validera en vald faktura"-flöde. Det innehåller
   ingen egen extraktions-/valideringslogik, bara ett anrop av
   `IInvoiceExtractor.ExtraheraAsync` följt av `IInvoiceValidator.Validera`,
   plus två inbyggda exempelfakturor (inbäddade som strängar, inte filer,
   så vyn fungerar oavsett hosting).
4. **`ExtraheradFaktura`/`Betalning`/`Radpost` är oförändrade** (fortfarande
   `init`-only, som i backend). Redigering i UI:t sker via C#
   `with`-uttryck (`faktura with { Fält = nyttVärde }`) snarare än att
   göra egenskaperna skrivbara – det höll den befintliga modellen orörd,
   i linje med "använd som de är".
5. **Fältmarkering av osäkerhet** kombinerar två källor: (a)
   `ExtraheradFaktura.OsakraFalt` (fältnamn direkt från modellen) och (b)
   enkel textmatchning mot de meddelandemönster `InvoiceValidator` själv
   genererar (t.ex. "Summan går inte ihop", "Organisationsnummer...").
   `GranskningsResultat.Avvikelser` är fri text utan fältkoppling i typen,
   så den här kopplingen är en UI-heuristik, inte ny valideringslogik.
   Ändras meddelandetexterna i `InvoiceValidator` behöver nyckelordslistan
   i `Granskning.razor` (`AvvikelseNycklar`) uppdateras i takt med det.
6. **`IExportService`** är en stub som loggar den godkända fakturan som
   indenterad JSON via `ILogger` – ingen riktig integration, enligt
   uppdraget.
7. **Två separata backend-fix från tidigare i vår konversation är
   inbakade**, eftersom UI:ts hela premiss (banner/godkänn-knapp) bygger
   på att `IInvoiceValidator` fungerar korrekt:
   - `GranskningsResultat.KraverGranskning` döptes om till
     `KräverGranskning` för att matcha den senaste specen.
   - En bugg där en bekräftande `noteringar`-text (utan att
     `granskning_kravs` eller `osakra_falt` var satta) ändå tvingade fram
     granskning är fixad.
8. **PDF:er skickas direkt till Claude, inte via lokal textextraktion.**
   Ett tidigare försök använde PdfPig för att extrahera text lokalt innan
   den skickades till Claude – det gav opålitliga resultat (siffror i fel
   ordning/kolumn på fakturor med tabeller, olika resultat mellan
   körningar på samma fil), eftersom textextraktion ur PDF:er med
   layout/tabeller är en heuristik, inte en garanti. Nu skickas PDF:en rå
   (base64, `content: [{ "type": "document", ... }, { "type": "text",
   ... }]`) och Claude läser den multimodalt – samma sätt en människa
   skulle läsa den. `IInvoiceExtractor` har därför två metoder:
   `ExtraheraAsync(string)` för text och `ExtraheraFranPdfAsync(byte[])`
   för PDF, som delar all HTTP-/felhanteringslogik men skiljer sig bara i
   hur user-meddelandet byggs. PdfPig-paketreferenserna är borttagna helt.
   Gränsen för uppladdning är fortsatt 8 MB (väl inom Anthropics
   PDF-gräns). Skannade/bildbaserade PDF:er utan textlager kan Claude
   fortfarande läsa (det är multimodalt, inte textbaserat) men kvaliteten
   beror då på bildupplösningen i PDF:en.
9. **`temperature: 0`** sätts på Anthropic-anropet. Extraktion är en
   strukturerad uppgift, inte kreativ text – samma faktura ska tolkas
   likadant varje gång, inte variera mellan körningar.
10. **`GranskningsResultat` delar nu upp hårda och mjuka signaler.**
    `Avvikelser`/`KräverGranskning` kommer enbart från C#:s egna kontroller
    (summa, organisationsnummer, datumordning) och blockerar godkännande.
    Modellens egna signaler (`osakra_falt`, `granskning_kravs`,
    `noteringar`) hamnar i ett nytt fält, `ModellNoteringar` – visas för
    granskaren men blockerar inte, eftersom modellen kan flagga saker som
    visar sig stämma vid närmare granskning (t.ex. en ovanlig men korrekt
    radpost). **Konsekvens för konsolappen:** exitkod 2 ("kräver
    granskning") utlöses nu bara av hårda C#-fel, inte längre av att
    modellen själv satt `granskning_kravs: true`. Modellens noteringar
    finns fortfarande med i JSON-utdatan, bara inte i exitkoden.
11. **Modellnoteringar rensas vid manuell redigering.** Redigerar
    granskaren ett fält (eller en radpost) för hand, nollställs
    `GranskningKravs`/`OsakraFalt`/`Noteringar` på fakturan innan den
    valideras om. Tanken: Claudes kommentar gällde sin egen, ursprungliga
    tolkning av fältet – när en människa skrivit över värdet är den
    kommentaren inte längre relevant. Det är avsiktligt en helomfattande
    rensning (alla fält, inte bara det redigerade) snarare än
    fält-för-fält, eftersom det var vad som efterfrågades; hör av dig om
    du vill ha mer finkornig kontroll. De hårda C#-kontrollerna
    (`Avvikelser`/`KräverGranskning`) räknas alltid om oavsett, och
    påverkas inte av rensningen.

## Projektstruktur (Blazor-delen)

```
FakturaExtraktion.Web/
  Program.cs                          DI (samma Anthropic-uppsättning som konsolappen) + Blazor-bootstrap
  appsettings.json                    Modellnamn, timeout, toleranser
  Components/
    App.razor, Routes.razor, _Imports.razor
    Layout/MainLayout.razor
    Pages/Granskning.razor            Huvudvyn: val, banner, tvåkolumnslayout, godkänn
    FaltRad.razor                     Återanvändbar fältrad med osäkerhets-markering
    Radpostabell.razor                Redigerbar radpost-tabell + summa
  Services/
    IFakturaService.cs / FakturaService.cs   Se antagande 3
    IExportService.cs / ExportService.cs     Exportstub, se antagande 6
  wwwroot/app.css                     All styling (designtoken + komponenter)
```
