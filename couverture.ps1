<#
.SYNOPSIS
    Livrable 5 — exécute la batterie xUnit, mesure la couverture avec coverlet
    et produit le rapport HTML avec ReportGenerator.

.DESCRIPTION
    Une seule commande pour la remise :

        ./couverture.ps1            # tests + couverture + rapport
        ./couverture.ps1 -Ouvrir    # ouvre le rapport HTML à la fin

    Le périmètre de mesure est défini dans coverlet.runsettings.
    ReportGenerator est un outil local, déclaré dans .config/dotnet-tools.json :
    aucune installation globale n'est nécessaire, `dotnet tool restore` suffit.

.PARAMETER SeuilBranches
    Pourcentage minimal de couverture de branche. Le script sort en erreur en
    dessous, ce qui rend une régression visible tout de suite au lieu de la
    laisser se découvrir à la remise. 0 = pas de seuil (valeur par défaut, à
    relever au fur et à mesure que la batterie grossit).

.PARAMETER Ouvrir
    Ouvre index.html dans le navigateur une fois le rapport généré.
#>
[CmdletBinding()]
param(
    [ValidateRange(0, 100)]
    [int]$SeuilBranches = 0,

    [switch]$Ouvrir
)

$ErrorActionPreference = 'Stop'

$racine    = $PSScriptRoot
$resultats = Join-Path $racine 'TestResults'
$rapport   = Join-Path $resultats 'rapport'

# --- 1. Repartir d'un dossier propre -----------------------------------------
# Sans cela, ReportGenerator agrégerait les cobertura des exécutions précédentes
# et afficherait un taux qui ne correspond à aucun état réel du code.
if (Test-Path $resultats) {
    Remove-Item $resultats -Recurse -Force
}

# --- 2. Outils locaux ---------------------------------------------------------
Write-Host '=> Restauration des outils locaux (ReportGenerator)' -ForegroundColor Cyan
dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore a échoué.' }

# --- 3. Tests + collecte coverlet --------------------------------------------
Write-Host '=> Exécution des tests xUnit avec collecte de couverture' -ForegroundColor Cyan
dotnet test (Join-Path $racine 'Tests\Tests.csproj') `
    --collect:"XPlat Code Coverage" `
    --settings (Join-Path $racine 'coverlet.runsettings') `
    --results-directory $resultats `
    --nologo
if ($LASTEXITCODE -ne 0) { throw 'Des tests ont échoué. Le rapport ne serait pas représentatif.' }

# coverlet écrit dans TestResults\<guid>\coverage.cobertura.xml
$cobertura = Get-ChildItem -Path $resultats -Filter 'coverage.cobertura.xml' -Recurse
if (-not $cobertura) { throw "Aucun fichier coverage.cobertura.xml produit sous $resultats." }

# --- 4. Rapport ---------------------------------------------------------------
Write-Host '=> Génération du rapport ReportGenerator' -ForegroundColor Cyan
dotnet reportgenerator `
    "-reports:$($cobertura.FullName -join ';')" `
    "-targetdir:$rapport" `
    "-reporttypes:Html;HtmlSummary;TextSummary;Badges" `
    "-title:SONDAGEAPI - couverture (livrable 5)" `
    "-assemblyfilters:+SONDAGEAPI"
if ($LASTEXITCODE -ne 0) { throw 'ReportGenerator a échoué.' }

# --- 5. Résumé et seuil -------------------------------------------------------
$resume = Join-Path $rapport 'Summary.txt'
if (Test-Path $resume) {
    Write-Host ''
    Get-Content $resume | Select-Object -First 22
}

Write-Host ''
Write-Host "Rapport HTML : $(Join-Path $rapport 'index.html')" -ForegroundColor Green

if ($SeuilBranches -gt 0 -and (Test-Path $resume)) {
    $ligne = Select-String -Path $resume -Pattern 'Branch coverage:\s*([0-9.,]+)%' | Select-Object -First 1
    if ($ligne) {
        $mesure = [double]($ligne.Matches[0].Groups[1].Value -replace ',', '.')
        if ($mesure -lt $SeuilBranches) {
            throw "Couverture de branche à $mesure %, sous le seuil de $SeuilBranches %."
        }
        Write-Host "Couverture de branche : $mesure % (seuil : $SeuilBranches %)" -ForegroundColor Green
    }
}

if ($Ouvrir) {
    Start-Process (Join-Path $rapport 'index.html')
}
