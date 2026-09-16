# Livrable 4 — Authentification des participants et unicité de la participation

Branche `max`. Approche **jeton d'invitation opaque à usage unique**.
F-O implémente en parallèle une approche **JWT** sur la branche `fo` : les deux sont conservées
jusqu'à l'arbitrage contre la grille de correction.

> L'exigence du guide se lit en **deux** parties : *authentifier* le participant, et *garantir
> l'unicité* de sa participation. La seconde n'est pas une propriété de l'authentification — aucun
> jeton, signé ou non, n'est « non rejouable » par nature. La garantie est dans le modèle de
> données.

## 1. Ce qui a été livré

### Fichiers ajoutés

| Fichier | Rôle |
| --- | --- |
| `Models/Survey.cs` | Le sondage |
| `Models/Invitation.cs` | Justificatif du participant **et** registre de participation |
| `Models/SurveyResponse.cs` | La réponse — sans lien vers le participant |
| `Contracts/SurveyContracts.cs` | Records de requête/réponse |
| `Security/Participants/ParticipantTokenDefaults.cs` | Nom du schéma, de l'en-tête, de la politique |
| `Security/Participants/ParticipantClaims.cs` | Constantes de revendications |
| `Security/Participants/InvitationToken.cs` | Génération CSPRNG + hachage SHA-256 |
| `Security/Participants/ParticipantTokenAuthenticationHandler.cs` | Le schéma d'authentification |
| `Security/Participants/ParticipantAuthExtensions.cs` | Enregistrement DI + politique |
| `OpenApi/ParticipantTokenSecuritySchemeTransformer.cs` | Schéma OpenAPI, exigence par opération |
| `Migrations/20260915234509_SurveyDomain.cs` | Schéma du domaine, supprime `Products` |

### Fichiers modifiés

- `Data/ApplicationDbContext.cs` — trois `DbSet`, `OnModelCreating` (index, longueurs, cascades)
- `Program.cs` — cinq endpoints, `UseAuthentication`/`UseAuthorization`
- `SONDAGEAPI.http` — scénario complet, dont les cas 401/409
- `Security/ApiKeyOptions.cs` — `MinLength(4)` → `MinLength(32)` (la doc annonçait déjà 32)
- `Security/ApiKeyMiddleware.cs` — `LogError` → `LogWarning` sur clé invalide

## 2. Décisions et justifications

### Deux mécanismes, jamais fusionnés

| Couche | Répond à | Durée de vie | Portée |
| --- | --- | --- | --- |
| Clé d'API (livrable 1) | *Ce client a-t-il le droit de parler à l'API ?* | longue | toute l'API |
| Jeton de participation (livrable 4) | *Quel humain, et a-t-il déjà répondu ?* | courte, nominative | une participation |

Les endpoints de participation exigent **les deux**. Fusionner les deux schémas donnerait au
participant — qui n'a besoin que de déposer une réponse — la clé qui ouvre l'API entière. C'est une
élévation de privilèges inscrite dans la conception.

### Jeton opaque plutôt que JWT

L'argument principal du JWT est l'absence d'état : le serveur valide une signature sans toucher à
la base. **Cet avantage ne s'applique pas ici** : on ne peut pas répondre à « cette personne a-t-elle
déjà participé ? » depuis une revendication signée, il faut de toute façon interroger la base. On
paierait donc la complexité du JWT (confusion d'algorithme, `alg: none`, clé de signature faible,
drapeaux de validation désactivés « pour que ça marche », absence de révocation avant expiration,
charge utile lisible par tous) sans en tirer le bénéfice.

Le jeton opaque supprime ces surfaces : pas de signature à attaquer, révocation par simple `UPDATE`,
et la lecture en base qu'on faisait déjà sert de contrôle d'authentification.

### 256 bits, SHA-256, jamais le jeton en clair

`RandomNumberGenerator.GetBytes(32)` puis encodage Base64Url (URL-safe, donc transportable dans un
lien courriel). Seule l'empreinte SHA-256 est persistée : une fuite de `app.db` ne permet d'usurper
personne. Le jeton en clair n'existe que dans la réponse à sa création, et ne peut pas être réaffiché.

**Pourquoi SHA-256 et non bcrypt/Argon2** — un KDF lent existe pour décourager la *devinette* de
secrets à faible entropie (mots de passe). Un jeton de 256 bits tirés d'un CSPRNG n'est pas
devinable : il n'y a ni dictionnaire, ni table arc-en-ciel. Le coût du KDF serait payé à chaque
requête sans rien apporter. Le hachage rapide est ici le choix correct.

### `AuthenticationHandler` plutôt que middleware

Le livrable 1 a pris la voie du middleware (adhésion implicite, exemption explicite). Ici l'inverse
est souhaitable : seuls deux endpoints sont concernés, donc l'adhésion explicite via
`RequireAuthorization` est plus sûre. On obtient en prime `HttpContext.User`, la composition avec
d'autres schémas (le JWT de F-O), et la démonstration qu'on maîtrise l'abstraction du framework et
pas seulement le middleware.

**Le handler ne consomme pas le jeton.** L'authentification identifie, elle ne modifie pas l'état :
sinon une simple lecture brûlerait la participation. La rédemption appartient au seul endpoint de
soumission. `GET /participation` le démontre — il est rejouable indéfiniment.

### Anonymat des réponses par construction

`SurveyResponse` n'a **aucune** clé étrangère vers `Invitation` ni vers un participant. Le registre
de participation (« l'invitation X a servi ») et le contenu des réponses sont deux tables sans lien.
Quiconque obtient la base ne peut pas désanonymiser les répondants.

Angle d'attaque résiduel traité : `Invitation.RedeemedAt` et `SurveyResponse.SubmittedAt` seraient
écrits à quelques millisecondes d'intervalle, donc corrélables. `RedeemedAt` est pour cette raison un
`DateOnly` et non un `DateTimeOffset`. On perd la finesse de l'audit sur les rédemptions — c'est le
compromis accepté.

## 3. Le cœur du livrable — la course TOCTOU

L'implémentation naïve est une faille :

```csharp
if (await db.Invitations.AnyAsync(i => i.Id == id && i.RedeemedAt == null) == false)
    return Results.Conflict();
// ... deux requêtes simultanées passent toutes deux ici
```

C'est un *time-of-check to time-of-use* : entre la vérification et l'écriture, une seconde requête
passe le même test. C'est ainsi que se produisent le bourrage d'urnes et la double dépense.

La condition est donc placée **dans** l'`UPDATE`, et c'est le nombre de lignes affectées qui tranche :

```sql
UPDATE "Invitations" SET "RedeemedAt" = @p
WHERE "Id" = @invitationId AND "RedeemedAt" IS NULL
```

Zéro ligne affectée ⇒ quelqu'un a gagné la course ⇒ 409. La base arbitre, pas l'application.
Le `if` applicatif n'est plus une garantie, seulement un message d'erreur courtois.

Rédemption et insertion de la réponse sont dans **la même transaction** : sinon un plantage entre les
deux consommerait la participation sans enregistrer la réponse — pire que la course corrigée.

## 4. Vérification

Contre une instance réelle (`dotnet run`, clé éphémère par variable d'environnement) :

| Cas | Attendu | Obtenu |
| --- | --- | --- |
| Sans clé d'API | 401 | 401 |
| Clé d'API seule, sans jeton | 401 | 401 |
| Jeton inconnu | 401 | 401 |
| Jeton expiré | 401 | 401 (message identique aux précédents) |
| Contenu vide | 400 | 400 |
| `GET /participation` avant réponse, deux fois | `false`, non consommé | `false`, `false` |
| 1re soumission | 201 | 201 |
| 2e soumission, même jeton | 409 | 409 |
| `GET /participation` après | `true` | `true` |
| Jeton du sondage A utilisé sur le sondage B | 403 | 403 |
| **10 soumissions simultanées, un seul jeton neuf** | **1 × 201, 9 × 409** | **1 × 201, 9 × 409** |

Contrôle en base après la course : **une seule ligne** dans `Responses`, `RedeemedAt = 2026-09-15`
(date seule, la protection contre la corrélation fonctionne). Aucune erreur `database is locked`.

Index vérifié dans la migration générée : `IX_Invitations_TokenHash ... unique: true`.

## 5. Suites

### Tests à écrire (Requis 5)

1. La course : N soumissions concurrentes, exactement un 201 — le test qui prouve le livrable
2. Jeton expiré ⇒ 401 (injecter un `TimeProvider` de test, il est déjà enregistré en DI pour ça)
3. Jeton inconnu, absent, dupliqué, vide ⇒ 401, **réponses identiques**
4. Jeton d'un autre sondage ⇒ 403
5. `GET /participation` rejoué ⇒ ne consomme pas
6. Sondage clos ⇒ 409
7. Contenu au-delà de `ContentMaxLength` ⇒ 400
8. Le jeton en clair n'apparaît **jamais** dans les journaux

### Éléments à porter dans les documents

- **Requis 6** — section 2 (les deux couches, le refus de les fusionner), section 3 (TOCTOU), le
  compromis anonymat/corrélation d'horodatage, et l'arbitrage JWT vs jeton opaque. Dire
  explicitement qu'on a compris que l'absence d'état du JWT n'apporte rien ici vaut mieux que de
  laisser croire à un choix par défaut.
- **Requis 9** — SQLite sérialise les écritures : la garantie tient, mais un déploiement réel
  demande PostgreSQL ou équivalent. Le motif `UPDATE` conditionnel s'y transpose tel quel.
  Mentionner aussi la rotation des jetons et la purge des invitations expirées.

### Points laissés ouverts

- Aucune purge des invitations expirées ou rédemées — la table croît indéfiniment.
- Aucune limitation de débit sur `POST /reponses` : un attaquant tenant un jeton valide ne peut pas
  répondre deux fois, mais rien ne l'empêche de marteler l'endpoint.
- L'émission d'invitations n'est protégée que par la clé d'API partagée : quiconque la détient peut
  émettre autant d'invitations qu'il veut, donc fabriquer autant de participations. C'est la limite
  du livrable 1 (secret statique unique, sans rotation) qui remonte ici — à nommer dans le Requis 6.
- Le domaine reste volontairement minimal : `SurveyResponse.Content` est une chaîne libre, il n'y a
  ni questions ni types de réponses. L'objet du livrable est l'unicité, pas la richesse du sondage.
