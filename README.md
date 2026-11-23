# Dagplanner

Een webapplicatie om activiteiten per dag en tijdsblok te plannen. Het project is bedoeld voor mensen die meer overzicht in hun dag willen, met ADHD als aanleiding voor de ontwikkeling. De applicatie is in ontwikkeling; hieronder staan alleen functies die al in de codebase aanwezig zijn.

## Wat werkt al?

- Bekijk een dagplanning met zes vaste tijdsblokken en navigeer tussen dagen.
- Voeg activiteiten toe en wijzig hun naam, beschrijving, datum en tijdsblok.
- Markeer activiteiten als belangrijk of voltooid en verwijder ze na bevestiging.
- Registreer een account en meld je aan. Planningen zijn per gebruiker gescheiden.

**Nog niet beschikbaar:** tijdsblokken zelf instellen. Er is in deze repository geen concrete roadmap voor volgende functies vastgelegd.

## Lokaal starten

### Vereisten

- .NET 10 SDK
- Docker met Docker Compose voor PostgreSQL
- Een vrije poort `5433` voor PostgreSQL en `5001` voor de webapplicatie

Voer vanuit de repositoryroot uit:

```bash
cp .env.example .env
```

Bestaat `.env` al, sla deze kopieerstap over.

Kies in `.env` een eigen lokaal databasewachtwoord. Zorg dat `POSTGRES_USER`, `POSTGRES_PASSWORD` en `POSTGRES_DB` overeenkomen met de gebruikersnaam, het wachtwoord en de databasenaam in `ConnectionStrings__DatabaseConnection`. Gebruik voor de verbindingsreeks poort `5433`; Docker Compose koppelt die aan PostgreSQL-poort `5432` in de container. `.env` wordt niet gecommit.

```bash
docker compose up -d
./scripts/run-local.sh
```

Open <https://localhost:5001>. Het script laadt `.env` en start de server met het HTTPS-ontwikkelprofiel. Vertrouw zo nodig eerst het lokale ontwikkelcertificaat met `dotnet dev-certs https --trust`.

In de ontwikkelomgeving past de server bij het starten EF Core-migraties toe en maakt voorbeelddata en demoaccounts aan. Registreer je liever een eigen account via `/register`, start de server dan na registratie opnieuw om de zes standaardtijdsblokken voor dat account te laten aanmaken. Registratie is momenteel ook buiten `Development` openbaar; beoordeel die toegangsregel vóór productiegebruik.

### Configuratie

| Instelling | Gebruik |
| --- | --- |
| `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` | Initialisatie van de lokale PostgreSQL-container via Docker Compose |
| `ConnectionStrings__DatabaseConnection` | Verbinding van de .NET-server met PostgreSQL op `localhost:5433` |

De dubbele underscore in de naam van de verbindingsreeks komt overeen met `ConnectionStrings:DatabaseConnection` in .NET-configuratie. Voor andere omgevingen moet de verbindingsreeks via omgevingsconfiguratie of een secret store worden aangeleverd; automatische migraties en voorbeelddata draaien alleen in `Development`.

## Ontwikkelen en testen

```bash
dotnet build Planning.sln
dotnet test Planning.sln --filter 'FullyQualifiedName!~Planning.E2E.Tests'
```

De tweede opdracht voert de domein-, service-, API- en clienttests uit. Houd Docker actief: een deel van de service- en API-tests start een tijdelijke PostgreSQL-container met Testcontainers.

Voor de browsertests is daarnaast een geïnstalleerde Google Chrome nodig. De tests starten zelf een tijdelijke database en webserver; de lokaal gestarte applicatie is daarvoor niet nodig.

```bash
dotnet test tests/Planning.E2E.Tests/Planning.E2E.Tests.csproj --settings playwright.chrome.runsettings
```

## Technische opbouw

| Project | Verantwoordelijkheid |
| --- | --- |
| `Planning.Client` | Blazor WebAssembly-interface en API-client |
| `Planning.Server` | ASP.NET Core-host, FastEndpoints-API en authenticatie |
| `Planning.Shared` | Gedeelde API-contracten en validatie |
| `Planning.Services` | Toepassingslogica voor de dagplanning |
| `Planning.Domain` | Domeinmodellen en regels |
| `Planning.Persistence` | EF Core, PostgreSQL-mapping en migraties |

De server levert zowel de client als de API via dezelfde origin. ASP.NET Core Identity gebruikt cookies voor aanmelding. De client en server delen DTO's en validatiecontracten; de services houden de planningslogica buiten de HTTP-endpoints. EF Core met Npgsql bewaart accounts en planningen in PostgreSQL. Serilog verzorgt gestructureerde logging.

Succesvolle en foutieve API-antwoorden staan apart beschreven in de [API-succescontracten](docs/api-success-responses.md) en het [API-foutcontract](docs/api-errors.md).
