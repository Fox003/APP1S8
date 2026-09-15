# Livrable 1 — Clé d'accès API + documentation OpenAPI

**Date :** 2026-09-15 · **Auteur :** Max (ParciIllusion) · **Branche :** `max`
**Portée :** Requis 1 (HTTPS + clé d'accès) et la partie sécurité du Requis 2 (OpenAPI/Swagger)

**État : terminé et vérifié.**

---

## 1. Ce qui a été livré

Deux mécanismes distincts, comme l'exige le guide — celui-ci est la **clé d'accès applicative**
(Requis 1). L'authentification des participants (Requis 4) reste à faire et ne doit pas être
fusionnée avec ce schéma.

- La clé est exigée sur **tous** les endpoints par défaut ; les exemptions sont explicites.
- Elle est déclarée dans le document OpenAPI, donc visible dans Swagger et exportable vers Postman.
- L'application **refuse de démarrer** si la clé n'est pas configurée ou est trop courte.

### Fichiers ajoutés

| Fichier | Rôle |
| --- | --- |
| `SONDAGEAPI/Security/ApiKeyOptions.cs` | Options liées à la section `Sondage`, validation par DataAnnotations |
| `SONDAGEAPI/Security/ApiKeyMiddleware.cs` | Vérification de l'en-tête `X-API-Key` |
| `SONDAGEAPI/Security/ApiKeyExtensions.cs` | `AddApiKeyAuthentication`, `UseApiKeyAuthentication`, `WithoutApiKey` |
| `SONDAGEAPI/Security/AllowAnonymousApiKeyAttribute.cs` | Marqueur de métadonnée pour exempter un endpoint |
| `SONDAGEAPI/OpenApi/ApiKeySecuritySchemeTransformer.cs` | Écrit `components.securitySchemes` + `security` |
| `SONDAGEAPI/OpenApi/OpenApiExtensions.cs` | `AddSondageOpenApi`, `UseSondageOpenApi` |

### Fichiers modifiés

- `Program.cs` — câblage, suppression de `/weatherforecast`, contrainte de route `{id:int}`
- `SONDAGEAPI.http` — pointe maintenant sur HTTPS et les deux vrais endpoints
- `SONDAGEAPI.csproj` — ajout de `UserSecretsId`

---

## 2. Décisions et justifications

> Ces justifications alimentent directement le Requis 6 (analyse d'impact) et le Requis 9
> (recommandations opérationnelles). Les recopier plutôt que les réinventer.

### Middleware plutôt qu'endpoint filter

Un **endpoint filter** est opt-in : un nouvel endpoint n'est pas protégé tant que personne ne pense
à lui attacher le filtre. C'est une défaillance par omission — l'API échoue en mode *ouvert*.

Le **middleware** échoue en mode *fermé* : tout nouvel endpoint est protégé automatiquement, et
chaque exemption est explicite et repérable par `grep WithoutApiKey`.

On n'a pas utilisé un `AuthenticationHandler` : la clé identifie *une application*, pas une
personne — il n'y a aucun `ClaimsPrincipal` à construire. Le vrai pipeline d'authentification est
réservé au Requis 4, ce qui garde les deux mécanismes structurellement séparés.

### Stockage de la clé — `dotnet user-secrets`

```powershell
dotnet user-secrets set "Sondage:ApiKey" "<valeur>" --project SONDAGEAPI
```

**À dire précisément dans le rapport :** user-secrets écrit du **JSON en clair** dans
`%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`. Ce n'est **pas** du chiffrement.
Le problème que ça règle est « le secret n'est pas dans le dépôt », rien de plus. Prétendre que la
clé est « stockée de façon sécurisée » serait une surinterprétation.

La vraie protection au repos (Key Vault ou équivalent) relève du Requis 9. À mentionner aussi :
une clé partagée, statique et sans expiration est le point faible du schéma — nommer la limite
soi-même vaut mieux que se la faire signaler.

Autres points :

- Les user-secrets ne sont chargés **qu'en environnement Development**. Ailleurs, il faut la
  variable d'environnement `Sondage__ApiKey` (double underscore).
- Le store est **par développeur** : F-O doit exécuter la commande de son côté.
- Prévoir une ligne dans le README, sinon un correcteur qui clone obtient un échec au démarrage
  sans explication.

### Génération de la clé

Utiliser un CSPRNG. **`Get-Random` est à proscrire** : il s'appuie sur `System.Random`, un PRNG non
cryptographique dont la sortie est prédictible.

```powershell
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RNGCryptoServiceProvider]::Create().GetBytes($bytes)
$key = ($bytes | ForEach-Object { $_.ToString('x2') }) -join ''   # hex, 64 car.
```

Préférer l'**hexadécimal** au base64 : le base64 contient `+`, `/` et `=`, sur lesquels un
double-clic de sélection s'arrête — on copie un fragment et on obtient un 401 qui ressemble à un bug
d'implémentation.

### Mesures de sécurité dans le middleware

| Mesure | Raison |
| --- | --- |
| SHA-256 des deux côtés puis `CryptographicOperations.FixedTimeEquals` | Comparaison à temps constant ; le hachage uniformise la longueur, donc celle de la clé ne fuit pas non plus |
| `values.Count != 1` | Rejette l'en-tête absent **et** dupliqué (un attaquant peut en envoyer deux) |
| Réponse 401 identique pour « absente » et « invalide » | Pas d'oracle exploitable |
| Aucune journalisation de la valeur présentée | Seul l'événement est journalisé |
| `ValidateOnStart()` | Clé absente ⇒ l'application ne démarre pas, plutôt que servir sans contrôle |

### Endpoints exemptés

| Endpoint | Exempté | Justification |
| --- | --- | --- |
| `/ping` | oui | Sonde de disponibilité ; ne révèle que « le service répond » |
| `/openapi/v1.json` | oui, **en Development seulement** | Sinon Swagger UI ne peut pas charger son schéma |
| `/api/products/{id:int}` | non | — |

Le bloc OpenAPI complet est à l'intérieur de `IsDevelopment()` : hors développement, il n'y a ni
Swagger ni schéma publié. Publier le schéma sans authentification fournirait à un attaquant la carte
complète de la surface d'attaque — le contrôle est donc l'environnement, et c'est un choix
délibéré, pas un défaut.

---

## 3. Pièges rencontrés

Cinq problèmes réels, dont trois n'étaient pas des erreurs de compilation. À conserver : ils
illustrent des mécanismes non évidents d'ASP.NET Core.

### 3.1 La position du middleware ne protège pas les endpoints

`app.MapOpenApi()` enregistre un **endpoint**, pas un middleware. Tous les endpoints s'exécutent à
la **fin** du pipeline, après chaque middleware — quelle que soit la ligne où `MapOpenApi()` est
appelé. Résultat observé :

```
/swagger/index.html  -> 200   (UseSwaggerUI est un vrai middleware, il respecte sa position)
/openapi/v1.json     -> 401   (endpoint : passe après la vérification de clé)
```

Swagger UI chargeait donc sa coquille HTML puis recevait un 401 sur son schéma : aucune opération
affichée, aucun en-tête envoyé. Corrigé par `app.MapOpenApi().WithoutApiKey();`.

**Leçon générale :** l'ordre des `Use*` ne contrôle pas l'accès aux endpoints. Le même mécanisme
peut se tromper dans le sens permissif. Garder les appels d'authentification **littéraux dans
`Program.cs`** plutôt que cachés derrière une méthode d'extension, pour que la séquence reste
visible.

### 3.2 `security: [{}]` — l'exigence sérialisée à vide

`new OpenApiSecuritySchemeReference("ApiKeyAuth")` sans document hôte produit un objet vide dans le
JSON, sans erreur. Le bouton *Authorize* apparaissait (le schéma existait dans `components`) mais
**aucune opération n'exigeait la clé**, donc Swagger n'envoyait jamais l'en-tête.

Correction — passer le document :

```csharp
[new OpenApiSecuritySchemeReference(SchemeName, document)] = new List<string>()
```

Attendu : `"security": [{"ApiKeyAuth": []}]`.

### 3.3 Swagger UI : « For 'id': Required field is not provided »

Message de validation **côté client**, pas une réponse de l'API. .NET 10 émet de l'OpenAPI **3.1**
par défaut, où un schéma peut lister plusieurs types. Le paramètre `id` sortait en
`"type": ["integer", "string"]`, une union que le validateur de Swagger UI ne sait pas évaluer :
le champ était considéré comme vide quelle que soit la saisie, et la requête ne partait jamais.

Deux corrections nécessaires — la première seule ne suffit pas (3.0 transformait l'union en
`anyOf`, tout aussi mal supporté) :

```csharp
options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;   // OpenApiExtensions
app.MapGet("/api/products/{id:int}", ...)                 // Program.cs
```

L'union venait de l'absence de contrainte de route. `{id:int}` donne un `"type": "integer"` propre,
**et** rejette `/api/products/abc` au routage (404) au lieu de laisser la valeur atteindre le model
binding — un rétrécissement d'entrée à la couche la plus précoce.

Le pin en 3.0 sert aussi au **Requis 3** : l'import OpenAPI 3.1 de Postman est également
défaillant. Décision à documenter : « on cible 3.0 pour la compatibilité outillage ».

### 3.4 Microsoft.OpenApi v1 vs v2

`Microsoft.AspNetCore.OpenApi` 10.0.12 tire **Microsoft.OpenApi 2.12**, une refonte. La plupart des
exemples en ligne visent la v1 et ne compilent pas.

| | v1 (1.6.x) | v2 (2.x) — le nôtre |
| --- | --- | --- |
| Espaces de noms | `Microsoft.OpenApi.Models`, `.Any`, `.Interfaces` | tout aplati dans `Microsoft.OpenApi` |
| Références | `.Reference` posée sur un objet concret vide | types dédiés `...Reference` |
| Valeurs libres | `IOpenApiAny`, `OpenApiString` | `System.Text.Json.Nodes.JsonNode` |
| Collections | pré-initialisées | nullables — d'où les `??=` |
| Spécification | 3.0 | 3.0 **et** 3.1 |

`Components.SecuritySchemes` est un `IDictionary<string, IOpenApiSecurityScheme>` (l'interface) :
la définition concrète et la référence implémentent toutes deux ce type.

### 3.5 Un navigateur ne peut pas envoyer d'en-tête personnalisé

Taper `https://localhost:7016/api/products/1` dans la barre d'adresse donnera **toujours** 401 : une
navigation ne peut porter aucun en-tête personnalisé. Ce n'est pas un défaut. Pour tester : Swagger
« Try it out », le fichier `.http`, Postman ou curl.

---

## 4. Vérification

Instance réelle, après redémarrage :

| Requête | Attendu | Obtenu |
| --- | --- | --- |
| `GET /ping`, sans clé | 200 | **200** |
| `GET /api/products/1`, sans clé | 401 | **401** |
| `GET /api/products/1`, clé valide | 200 | **200** — `{"id":1,"name":"Produit Test","price":19.99}` |
| `GET /api/products/1`, clé erronée | 401 | **401**, réponse identique au cas « sans clé » |
| `GET /api/products/1` via Swagger UI | 200 | **200** (confirmé par Max) |
| Document OpenAPI | `security: [{"ApiKeyAuth": []}]` | **conforme**, `openapi: 3.0.4` |

Compilation : `0 avertissement, 0 erreur`.

**Reste à vérifier :** démarrage refusé quand la clé est absente ou trop courte. C'est la mitigation
la plus souvent revendiquée et jamais testée — à transformer en test xUnit pour le Requis 5.

---

## 5. Suites

### Tests à écrire (Requis 5)

Le middleware est testable des deux façons : en unitaire sur un `DefaultHttpContext` fabriqué (sans
hôte ni base), et en intégration via `WebApplicationFactory`.

- en-tête absent ⇒ 401
- clé erronée ⇒ 401, corps identique au cas précédent
- clé valide ⇒ 200
- en-tête dupliqué ⇒ 401
- endpoint marqué `WithoutApiKey` ⇒ 200 sans clé
- configuration absente ⇒ échec au démarrage

Pour `WebApplicationFactory<Program>`, la classe `Program` implicite est `internal` : il faudra
`public partial class Program { }` en bas de `Program.cs`, ou `<InternalsVisibleTo Include="Tests" />`.

### Éléments à porter dans les documents

- **Requis 6** — contrôles documentés mais non implémentés (un `securitySchemes` est une
  *affirmation* sur le serveur, que rien ne vérifie) ; position du middleware vs endpoints ;
  journalisation à volume non borné sur un chemin non authentifié (la mitigation est le rate
  limiting, et c'est un bon test pour le Requis 5).
- **Requis 9** — user-secrets ≠ chiffrement ; rotation de la clé ; ni Swagger ni schéma hors
  développement.
- **Requis 3** — importer `/openapi/v1.json` (document unique depuis la suppression de
  `AddSwaggerGen`, ce qui règle le problème connu n° 8).

### Points laissés ouverts

- `LogError` sur clé refusée : `LogWarning` correspond mieux à la sévérité (comportement client
  attendu, pas une faute serveur).
- `ApiKeyOptions` est `public` alors que le reste de `Security/` est `internal`.
- `Models/Product.cs`, `DbSet<Product>` et la migration `InitialCreate` restent du gabarit EF. Ils
  portent le seul endpoint protégé qui prouve le middleware : les supprimer **avec** les entités du
  domaine sondage, en une seule migration.
