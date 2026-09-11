# Nomenclature des Dépendances et Outils du Projet API

Ce document détaille l'ensemble des packages NuGet, bibliothèques et outils CLI intégrés au projet **APP1S8**, en précisant leur rôle technique et le requis du cahier des charges auquel ils répondent.

---

## 1. Vue d'ensemble des Dépendances

| Catégorie | Package / Outil | Version cible | Requis associé |
| :--- | :--- | :--- | :--- |
| **Documentation API** | `Swashbuckle.AspNetCore` | `7.x.x` | **Requis 2** (OpenAPI / Swagger) |
| **Sécurité & Auth** | `Microsoft.AspNetCore.Authentication.JwtBearer` | `10.x.x` | **Requis 4** (Authentification unique) |
| **Tests Unitaires** | `xunit` | `2.9.x` | **Requis 5** (Batterie de tests xUnit) |
| **Exécution des Tests** | `Microsoft.NET.Test.Sdk` | `17.x.x` | **Requis 5** (Environnement testhost) |
| **Runner Visual Studio / Rider** | `xunit.runner.visualstudio` | `3.x.x` | **Requis 5** (Intégration IDE) |
| **Couverture de Code** | `coverlet.collector` | `6.x.x` | **Requis 5** (Analyse de couverture) |
| **Simulations & Mocks** | `Moq` | `4.20.x` | **Requis 5** (Tests d'isolation) |
| **Protection du Code** | `Obfuscar.GlobalTool` | `2.2.x` | **Requis 8** (Obfuscation assemblies) |
| **Audit & Conformité** | `CycloneDX` | `3.x.x` | **Requis 10** (Rapport SBOM) |

---

## 2. Description Détaillée par Composant

### 📄 Documentation & Interface API

#### `Swashbuckle.AspNetCore`
* **Rôle :** Génère automatiquement la spécification **OpenAPI** (format JSON/YAML) à partir des contrôleurs C# et fournit une interface graphique interactive (**Swagger UI**) accessible via la route `/swagger`.
* **Requis :** **Requis 2** (Documentation d'API OpenAPI/Swagger).
* **Usage dans le projet :**
  ```csharp
  builder.Services.AddEndpointsApiExplorer();
  builder.Services.AddSwaggerGen();
  ```

---

### 🔐 Sécurité & Authentification

#### `Microsoft.AspNetCore.Authentication.JwtBearer`
* **Rôle :** Middleware responsable de la vérification et du décodage des jetons **JWT (JSON Web Tokens)** transmis dans les en-têtes HTTP `Authorization: Bearer <token>`. Il permet de valider l'identité et l'unicité des participants.
* **Requis :** **Requis 4** (Authentification des participants).
* **Usage dans le projet :**
  ```csharp
  builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
         .AddJwtBearer(options => { /* validation des clés */ });
  ```

---

### 🧪 Batterie de Tests & Couverture de Code

#### `xunit`
* **Rôle :** Framework de tests unitaires et d'intégration pour .NET. Permet de définir des méthodes de test au moyen des attributs `[Fact]` (test simple) et `[Theory]` (test paramétré).
* **Requis :** **Requis 5** (Tests automatisés).

#### `Microsoft.NET.Test.Sdk`
* **Rôle :** Fournit le moteur d'exécution de tests (`testhost.dll`) requis par la CLI .NET (`dotnet test`) et les environnements d'intégration pour traiter les assemblies de test.
* **Requis :** **Requis 5** (Moteur d'exécution des tests).

#### `xunit.runner.visualstudio`
* **Rôle :** Adaptateur permettant la découverte et l'exécution directe des tests xUnit à partir des explorateurs de tests intégrés à **JetBrains Rider**, **VS Code** et **Visual Studio**.
* **Requis :** **Requis 5** (Intégration IDE).

#### `coverlet.collector`
* **Rôle :** Collecteur de données de couverture de code cross-platform. Lors de l'exécution de la commande `dotnet test --collect:"XPlat Code Coverage"`, il analyse les lignes et branches de code exécutées par la suite de tests.
* **Requis :** **Requis 5** (Prouver la couverture complète du code).

#### `Moq`
* **Rôle :** Framework de création d'objets factices (*mocking*). Il permet de simuler des dépendances externes (bases de données, services tiers, requêtes HTTP) pour tester la logique de sécurité et les contrôleurs en isolation.
* **Requis :** **Requis 5** (Mitigation et vérification de la surface d'attaque).

---

### 🛡️ Obfuscation & Hardening

#### `Obfuscar.GlobalTool`
* **Rôle :** Outil CLI global qui prend les assemblages compiled (`.dll`) et leur applique des transformations (renommage de classes/méthodes, chiffrement des chaînes de caractères, obfuscation du flux de contrôle) selon la configuration définie dans `obfuscar.xml`.
* **Requis :** **Requis 8** (Protection du code par obfuscation).
* **Usage :**
  ```bash
  obfuscar.console obj/Debug/net10.0/obfuscar.xml
  ```

---

### 📦 Audit & Nomenclature Logicielle (SBOM)

#### `CycloneDX` (`dotnet-CycloneDX`)
* **Rôle :** Générateur de nomenclature logicielle (Software Bill of Materials - SBOM). Il inspecte le graphe complet des dépendances Directes et Transitives du projet .NET et génère un inventaire structuré au format standardisé CycloneDX JSON/XML.
* **Requis :** **Requis 10** (Rapport de nomenclature logicielle CycloneDX).
* **Usage :**
  ```bash
  dotnet cyclonedx APP1S8.csproj -o ./sbom -f json
  ```

---

## 3. Synthèse de Récapitulation `.csproj`

Voici l'extrait type des références NuGet de votre fichier de projet :

```xml
<ItemGroup>
  <!-- Documentation & Sécurité -->
  <PackageReference Include="Swashbuckle.AspNetCore" Version="7.0.0" />
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />

  <!-- Framework de Test & Couverture -->
  <PackageReference Include="xunit" Version="2.9.2" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
  <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
  <PackageReference Include="coverlet.collector" Version="6.0.2" />
  <PackageReference Include="Moq" Version="4.20.72" />
</ItemGroup>
```
