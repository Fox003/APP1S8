# Livrable 5 — Batterie de tests xUnit et couverture de branche à 100 %

**Date :** 2026-09-17 · **Auteur :** Max (ParciIllusion) · **Branche :** `max`
**Portée :** Requis 5 (tests unitaires xUnit, couverture de branche de 100 %, coverlet,
ReportGenerator)

**État : terminé et vérifié.** 117 tests, 100 % de lignes, 100 % de branches (30/30),
100 % de méthodes (25/25) d'après ReportGenerator.

---

## 1. Ce qui a été livré

```
Tests/
  Infrastructure/
    SondageApiFactory.cs        hôte de test (WebApplicationFactory<Program>)
    ApiFactoryVariants.cs       variantes : Production, port HTTPS, revendications falsifiées
    TestTimeProvider.cs         horloge pilotée
    ApiScenario.cs              raccourcis de mise en situation
  ApiKeyMiddlewareTests.cs                    livrable 1
  HttpsRedirectionTests.cs                    livrable 1
  ApiKeyOptionsTests.cs                       livrable 1 — validation au démarrage
  SurveyEndpointsTests.cs                     administration des sondages
  InvitationEndpointsTests.cs                 émission des jetons
  ParticipationEndpointsTests.cs              livrable 4
  ParticipationUniquenessTests.cs             livrable 4 — course TOCTOU
  ParticipantTokenLifetimeTests.cs            expiration + sondage clos
  ForgedClaimsTests.cs                        défense en profondeur des endpoints
  ParticipantTokenAuthenticationHandlerTests.cs  réponses de rejet du gestionnaire
  InvitationTokenTests.cs                     qualité du jeton
  OpenApiDocumentTests.cs                     livrable 2 — document réellement produit
  OpenApiTransformerTests.cs                  livrable 2 — transformateurs isolés
Tests/Tests.csproj            périmètre de mesure + seuil (PropertyGroup coverlet)
.run/                         configurations Rider partagées, dossier « Couverture »
.config/dotnet-tools.json     ReportGenerator en outil local
```

Dans Rider, dossier **Couverture** de la liste des configurations. Lancer
**« 3 - Rapport »** (ou **« 4 - Ouvrir le rapport »**) déclenche toute la chaîne par les tâches
*before launch* :

| Configuration | Commande |
| --- | --- |
| 1 - Outils locaux | `dotnet tool restore` |
| 2 - Tests | `dotnet test Tests/Tests.csproj -p:CollectCoverage=true` |
| 3 - Rapport | `dotnet reportgenerator "-reports:TestResults/coverage.cobertura.xml" …` |
| 4 - Ouvrir le rapport | ouvre `TestResults/rapport/index.html` |

L'étape 2 échoue si la couverture de **ligne ou de branche** descend sous 100 % — c'est coverlet
lui-même qui applique le seuil, à partir de ses propres compteurs. La régression se voit donc au
lancement, au lieu de se découvrir à la remise.

Les configurations vivent dans `.run/` à la racine, versionné : `.idea/` est ignoré par git, donc
une configuration créée à la main dans Rider ne suivrait pas jusqu'à F-O.

---

## 2. Le problème de pile qu'il a fallu trancher

Le guide impose **coverlet** (« l'intégration coverlet incluse automatiquement dans la création de
projet xUnit ») et **ReportGenerator**. Le projet `Tests` tel que F-O l'avait créé utilisait
`xunit.v3.mtp-v2`, c'est-à-dire xUnit v3 sur **Microsoft.Testing.Platform**.

Les deux sont incompatibles, et le SDK .NET 10 le dit franchement :

```
error : Testing with VSTest target is no longer supported by Microsoft.Testing.Platform
        on .NET 10 SDK and later.
```

- coverlet s'accroche à VSTest, que ce soit par `coverlet.collector` (collecteur de données) ou par
  `coverlet.msbuild` (cible MSBuild greffée sur `VSTest`). Il n'existe pas d'extension coverlet pour
  MTP, ni dans un cas ni dans l'autre.
- Tous les paquets `xunit.v3*` en version 4.x tirent `Microsoft.Testing.Platform.MSBuild`,
  y compris `xunit.v3.core` seul — vérifié avec `dotnet nuget why`. Il n'y a pas de variante
  « v3 sans MTP ».

Trois issues possibles :

| Option | Verdict |
| --- | --- |
| Garder MTP + `Microsoft.Testing.Extensions.CodeCoverage` | Rejetée : ce n'est pas coverlet, et le guide nomme coverlet |
| Garder MTP + `coverlet.console` en outil externe | Rejetée : coverlet, oui, mais pas « l'intégration incluse dans le projet xUnit » |
| **Revenir au lanceur VSTest avec xUnit 2** | **Retenue** |

`Tests` utilise donc désormais `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.4,
`Microsoft.NET.Test.Sdk` 18.0.1 et `coverlet.msbuild` 6.0.4 — à une nuance près, la pile que
produisait le modèle `dotnet new xunit` auquel le guide fait référence. La nuance : le modèle livre
`coverlet.collector`, et le collecteur **ignore en silence** l'élément `<Threshold>` d'un fichier
`.runsettings` (vérifié : une exécution partielle à 1,63 % de couverture sort quand même en code 0).
Seule l'intégration `coverlet.msbuild` sait collecter *et* faire échouer la commande sous le seuil,
d'où le passage de l'une à l'autre le 2026-09-17, lors du remplacement de `couverture.ps1` par les
configurations Rider. Le bloc `"test": { "runner": ... }` du
`global.json` racine a été retiré ; sans cela, `dotnet test` continuerait de chercher MTP.

Conséquence à connaître : c'est un retour en arrière assumé par rapport à la direction que prend le
SDK. Si le cours change de consigne un jour, la migration inverse est mécanique (remettre
`xunit.v3.mtp-v2` et `Microsoft.Testing.Extensions.CodeCoverage`), mais elle coûte le rapport
coverlet.

---

## 3. L'hôte de test

`SondageApiFactory` démarre **l'API réelle** en mémoire via `WebApplicationFactory<Program>`. Tout le
pipeline s'exécute : redirection HTTPS, middleware de clé d'API, schéma d'authentification du
participant, endpoints. Deux choses seulement sont substituées.

**La base.** Un fichier SQLite temporaire, unique par classe de test, injecté par la configuration
(`ConnectionStrings:DefaultConnection`). Délibérément **pas** le fournisseur InMemory d'EF Core :
la garantie d'unicité repose sur `ExecuteUpdateAsync` dans une transaction, qu'InMemory ne sait pas
reproduire. Tester l'unicité sur InMemory reviendrait à tester une garantie qui n'existe pas.

**L'horloge.** `TestTimeProvider` remplace `TimeProvider.System`. Sans elle, la branche « jeton
expiré » ne serait jamais exercée — il faudrait attendre un jour.

La clé d'API est fournie par une source de configuration en mémoire ajoutée en dernier, donc
prioritaire sur `appsettings.json` **et** sur le magasin de secrets du développeur. La suite ne
dépend d'aucune configuration locale : elle tourne à l'identique chez F-O et en CI.

### Trois pièges rencontrés

1. **`SqliteConnection.ClearAllPools()` est global au processus.** Appelé dans `Dispose` d'une
   fabrique, il coupait les connexions des classes de test qui tournaient en parallèle — d'où des
   500 aléatoires, reproductibles seulement en suite complète. Remplacé par `ClearPool(connection)`,
   qui ne vide que le pool de cette chaîne de connexion.
2. **`WebApplicationFactory<Program>` exige un `Program` accessible.** Les instructions de haut
   niveau en génèrent un interne. Ajouté `public partial class Program;` à la fin de `Program.cs`
   (motif documenté par ASP.NET Core) plus `<InternalsVisibleTo Include="Tests" />` dans le csproj
   pour atteindre `ApiKeyMiddleware`, `InvitationToken` et les transformateurs. À signaler au
   livrable 6, et à vérifier contre la configuration d'obfuscation du livrable 8.
3. **`ConfigureTestServices` et non `ConfigureServices`** : seul le premier s'exécute après les
   enregistrements de `Program.cs`, donc seul lui permet de remplacer le `TimeProvider` posé par
   `TryAddSingleton`.

---

## 4. Les mitigations prouvées

Ce tableau est la matière première du livrable 6 : chaque ligne est un vecteur d'attaque et le test
qui démontre qu'il est fermé.

| Vecteur | Mitigation | Test |
| --- | --- | --- |
| Appel non authentifié | Middleware clé d'API sur tout `/api/` | `Sans_en_tete_la_requete_est_refusee` |
| Clé devinée | Comparaison à temps constant sur empreinte SHA-256 | `Avec_une_mauvaise_cle_la_requete_est_refusee` |
| Oracle d'erreur sur la clé | 401 identique si absente ou fausse | `Les_refus_sont_indiscernables_entre_cle_absente_et_cle_fausse` |
| Contrebande d'en-tête | `values.Count != 1` refusé | `Un_en_tete_duplique_est_refuse_meme_si_une_valeur_est_bonne` |
| Interception réseau | `UseHttpsRedirection` | `Une_requete_HTTP_est_redirigee_vers_HTTPS` |
| Démarrage sans secret | `ValidateOnStart` + `MinLength(32)` | `ApiKeyOptionsTests` (4 cas) |
| **Double participation (TOCTOU)** | `UPDATE` conditionnel + transaction | `Dix_soumissions_simultanees_du_meme_jeton_nen_laissent_passer_quune` |
| Rejeu d'un jeton consommé | Même mécanisme, en séquentiel | `Un_jeton_deja_utilise_est_refuse_en_409` |
| Déplacement latéral entre sondages | `tokenSurveyId != id` → 403 | `Un_jeton_ne_donne_acces_quau_sondage_qui_la_emis` |
| Jeton inventé | Recherche par empreinte sur index unique | `Un_jeton_inconnu_est_refuse_en_401` |
| Oracle d'existence de jeton | 401 identique si absent, inconnu ou expiré | `Un_jeton_expire_est_indiscernable_dun_jeton_inconnu` |
| Jeton périmé | Comparaison `ExpiresAt <= now` | `ParticipantTokenLifetimeTests` (4 cas) |
| Réponse après clôture | `ClosesAt <= now` → 409 | `ClosedSurveyTests` (3 cas) |
| Fuite de la base | Seule l'empreinte est persistée | `Seule_lempreinte_du_jeton_est_persistee` |
| Désanonymisation | `RedeemedAt` en `DateOnly`, 201 sans identifiant | `La_redemption_nest_datee_quau_jour_pres`, `La_reponse_201_ne_divulgue_aucun_identifiant_de_reponse` |
| Injection SQL | Requêtes paramétrées par EF Core | `Un_titre_portant_une_injection_SQL_est_stocke_litteralement` (+ équivalent sur les réponses) |
| Charge utile démesurée | Bornes de longueur titre/réponse/validité | `SurveyEndpointsTests`, `InvitationEndpointsTests` |
| Revendication falsifiée | Reconversion en GUID côté endpoint | `ForgedClaimsTests` (5 cas) |
| Publication du schéma en production | `UseSondageOpenApi` conditionné à Development | `ProductionPipelineTests` |
| Fuite de trace d'exécution | Pas de page d'exception hors Development | `Un_corps_JSON_malforme_ne_divulgue_aucune_trace_dexecution` |

---

## 5. Comment le 100 % de branche a été atteint

Quatre branches n'étaient atteignables par aucune requête HTTP. Les couvrir a demandé des montages
particuliers, chacun justifié par un scénario réel — pas par la seule envie d'un chiffre rond.

**`TryGetParticipant` renvoyant `false`.** La politique d'autorisation n'exige que la *présence* de
la revendication `invitation_id` ; elle ne dit rien de son format. Le gestionnaire actuel émet
toujours un GUID valide, donc la garde ne se déclenche jamais. `ForgedClaimsApiFactory` substitue le
gestionnaire par un gestionnaire qui émet des revendications malformées — exactement ce que
produirait le schéma JWT de F-O composé avec celui-ci, ou un gestionnaire compromis. La substitution
passe par `PostConfigure<AuthenticationOptions>` en mutant le `HandlerType` du
`AuthenticationSchemeBuilder` déjà enregistré : `AddScheme` refuserait un second enregistrement du
même nom, mais `SchemeMap` et la liste lue par `AuthenticationSchemeProvider` partagent le même
objet, donc muter suffit.

**`HandleForbiddenAsync` du gestionnaire.** Mort dans l'API actuelle, pour la même raison. Testé
directement : `handler.InitializeAsync(...)` puis `handler.ForbidAsync(null)`, et on lit le corps de
la réponse. Le jour où une exigence s'ajoute à la politique, ce chemin s'allume — autant qu'il soit
déjà correct.

**Le sondage introuvable à la soumission.** Il faut une invitation orpheline, que la cascade de
SQLite interdit de produire. `ExecuteWithoutForeignKeysAsync` supprime le sondage avec
`PRAGMA foreign_keys = OFF` sur une connexion à part, ce qui laisse l'invitation en place. Une
migration bâclée produirait le même état.

**Les transformateurs OpenAPI.** Leurs `??=` ne voient qu'un seul côté dans le pipeline réel
(`Components` toujours non nul, `Security` toujours nul), et la branche `JsonSchemaType.Number` ne
s'active qu'avec un `double` ou un `decimal` exposé — aucun contrat n'en a. `OpenApiTransformerTests`
les appelle directement sur des documents et schémas construits à la main. Les contextes
(`OpenApiDocumentTransformerContext`, `OpenApiOperationTransformerContext`,
`OpenApiSchemaTransformerContext`) sont constructibles par initialiseur d'objet.

### Le périmètre de mesure

Le `PropertyGroup` de `Tests/Tests.csproj`, conditionné à `CollectCoverage=true`, exclut
`Migrations/` et `obj/`. Les migrations sont générées par
`dotnet ef` et réécrites à chaque changement de modèle : les mesurer reviendrait à mesurer EF Core,
et la moindre migration ferait chuter le taux sans qu'une ligne écrite à la main change. `obj/`
contient le code des générateurs de source (le support des commentaires XML d'OpenAPI). `Include`
est limité à `[SONDAGEAPI]*` : couvrir le projet de tests lui-même gonflerait le résultat sans rien
prouver. `SkipAutoProps` écarte les accesseurs triviaux des modèles et des contrats, qui n'ont
aucune branche.

**À dire au correcteur si la question vient :** ces exclusions sont documentées dans le fichier
lui-même, et le taux porte sur 429 lignes de code écrites à la main, pas sur un périmètre taillé
pour flatter le chiffre.

---

## 6. Deux constats à reprendre au livrable 6

**La page d'exception du développeur divulgue tout.** En Development, un corps JSON malformé renvoie
un 400 dont le corps contient la trace d'exécution complète, noms de types internes compris. C'est le
comportement normal de `WebApplication` en Development, et c'est acceptable en local — mais cela
signifie qu'un déploiement où `ASPNETCORE_ENVIRONMENT` resterait à `Development` divulguerait la
structure interne de l'API à n'importe quel client. `ProductionPipelineTests` prouve que ce n'est pas
le cas hors Development ; la recommandation opérationnelle (livrable 9) doit nommer explicitement la
variable d'environnement.

**`UseHttpsRedirection` protège la requête *suivante*.** Une requête HTTP porteuse de la clé d'API
reçoit bien un 307 avant d'atteindre le middleware — mais la clé, elle, a déjà voyagé en clair sur
le réseau. La redirection ne répare pas la première requête ; seul HSTS, côté client, empêche qu'elle
parte. `UseHsts()` n'est pas dans le pipeline actuel. À nommer dans l'analyse plutôt que de laisser
un correcteur le trouver.

---

## 7. Modifications du code de production

Minimales, et toutes au service de la testabilité :

| Fichier | Modification | Pourquoi |
| --- | --- | --- |
| `SONDAGEAPI/Program.cs` | `public partial class Program;` en fin de fichier | Point d'entrée de `WebApplicationFactory` |
| `SONDAGEAPI/SONDAGEAPI.csproj` | `<InternalsVisibleTo Include="Tests" />` | Accès aux types internes testés |
| `global.json` (racine) | Reçoit le bloc `sdk`, perd `test.runner` | Épingle le SDK pour toute la solution ; libère le lanceur VSTest |
| `SONDAGEAPI/global.json` | Supprimé | Redondant, et sous la solution donc jamais lu |

**Aucune logique métier ni de sécurité n'a été modifiée.** Les 117 tests décrivent le comportement
tel qu'il était déjà.

---

## 8. Reste à faire

- Décider entre le jeton opaque (`max`) et le JWT (`fo`) pour le livrable 4. Si le JWT l'emporte,
  toute la partie `ParticipationEndpointsTests` / `ParticipationUniquenessTests` reste valable —
  seule l'obtention du jeton change — mais `ForgedClaimsTests` devient un test de production, pas
  une hypothèse.
- Livrable 6 : reprendre le tableau de la section 4 et les deux constats de la section 6.
- Livrable 8 : vérifier que l'obfuscation ne casse ni `public partial class Program` ni
  `InternalsVisibleTo`.
- CI : `dotnet test Tests/Tests.csproj -p:CollectCoverage=true` est prêt à être appelé tel quel ;
  il échoue sous 100 % de ligne **ou** de branche, sans dépendre de Rider ni d'un script maison.
