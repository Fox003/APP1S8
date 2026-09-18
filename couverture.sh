#!/usr/bin/env bash
#
# Livrable 5 — exécute la batterie xUnit, mesure la couverture avec coverlet
# et produit le rapport HTML avec ReportGenerator.
#
# Usage :
#   ./couverture.sh                        # tests + couverture + rapport
#   ./couverture.sh --ouvrir                # ouvre le rapport HTML à la fin
#   ./couverture.sh --seuil-branches 80     # échoue si couverture de branche < 80%
#   ./couverture.sh -s 80 -o                # les deux combinés
#
# Le périmètre de mesure est défini dans coverlet.runsettings.
# ReportGenerator est un outil local, déclaré dans .config/dotnet-tools.json :
# aucune installation globale n'est nécessaire, `dotnet tool restore` suffit.

set -euo pipefail

# --- 0. Arguments -------------------------------------------------------------
seuil_branches=0
ouvrir=0

usage() {
    echo "Usage: $0 [-s|--seuil-branches N] [-o|--ouvrir]"
    exit 1
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -s|--seuil-branches)
            seuil_branches="${2:-}"
            [[ -z "$seuil_branches" ]] && usage
            shift 2
            ;;
        -o|--ouvrir)
            ouvrir=1
            shift
            ;;
        -h|--help)
            usage
            ;;
        *)
            echo "Argument inconnu : $1" >&2
            usage
            ;;
    esac
done

if ! [[ "$seuil_branches" =~ ^[0-9]+$ ]] || [ "$seuil_branches" -lt 0 ] || [ "$seuil_branches" -gt 100 ]; then
    echo "Erreur : --seuil-branches doit être un entier entre 0 et 100." >&2
    exit 1
fi

# Couleurs (désactivées si la sortie n'est pas un terminal)
if [[ -t 1 ]]; then
    CYAN=$'\033[0;36m'
    GREEN=$'\033[0;32m'
    RESET=$'\033[0m'
else
    CYAN=''; GREEN=''; RESET=''
fi

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
resultats="$racine/TestResults"
rapport="$resultats/rapport"

# --- 1. Repartir d'un dossier propre -----------------------------------------
# Sans cela, ReportGenerator agrégerait les cobertura des exécutions précédentes
# et afficherait un taux qui ne correspond à aucun état réel du code.
if [[ -d "$resultats" ]]; then
    rm -rf "$resultats"
fi

# --- 2. Outils locaux ---------------------------------------------------------
echo "${CYAN}=> Restauration des outils locaux (ReportGenerator)${RESET}"
if ! dotnet tool restore; then
    echo "dotnet tool restore a échoué." >&2
    exit 1
fi

# --- 3. Tests + collecte coverlet --------------------------------------------
echo "${CYAN}=> Exécution des tests xUnit avec collecte de couverture${RESET}"
if ! dotnet test "$racine/Tests/Tests.csproj" \
    --collect:"XPlat Code Coverage" \
    --settings "$racine/coverlet.runsettings" \
    --results-directory "$resultats" \
    --nologo; then
    echo "Des tests ont échoué. Le rapport ne serait pas représentatif." >&2
    exit 1
fi

# coverlet écrit dans TestResults/<guid>/coverage.cobertura.xml
# (on évite `mapfile`, absent du bash 3.2 fourni par défaut sur macOS)
cobertura_files=()
while IFS= read -r -d '' fichier; do
    cobertura_files+=("$fichier")
done < <(find "$resultats" -type f -name 'coverage.cobertura.xml' -print0)

if [[ ${#cobertura_files[@]} -eq 0 ]]; then
    echo "Aucun fichier coverage.cobertura.xml produit sous $resultats." >&2
    exit 1
fi

# Joindre les chemins avec ';' comme attend ReportGenerator
cobertura_joined=$(IFS=';'; echo "${cobertura_files[*]}")

# --- 4. Rapport ---------------------------------------------------------------
echo "${CYAN}=> Génération du rapport ReportGenerator${RESET}"
if ! dotnet reportgenerator \
    "-reports:$cobertura_joined" \
    "-targetdir:$rapport" \
    "-reporttypes:Html;HtmlSummary;TextSummary;Badges" \
    "-title:SONDAGEAPI - couverture (livrable 5)" \
    "-assemblyfilters:+SONDAGEAPI"; then
    echo "ReportGenerator a échoué." >&2
    exit 1
fi

# --- 5. Résumé et seuil -------------------------------------------------------
resume="$rapport/Summary.txt"
if [[ -f "$resume" ]]; then
    echo ""
    head -n 22 "$resume"
fi

echo ""
echo "${GREEN}Rapport HTML : $rapport/index.html${RESET}"

if [[ "$seuil_branches" -gt 0 && -f "$resume" ]]; then
    ligne=$(grep -m 1 -E 'Branch coverage:\s*[0-9.,]+%' "$resume" || true)
    if [[ -n "$ligne" ]]; then
        brut=$(echo "$ligne" | grep -oE '[0-9.,]+%' | head -n 1 | tr -d '%')
        mesure=${brut//,/.}
        # Comparaison flottante avec awk (bash ne gère pas les décimaux nativement)
        if awk -v m="$mesure" -v s="$seuil_branches" 'BEGIN { exit !(m < s) }'; then
            echo "Couverture de branche à $mesure %, sous le seuil de $seuil_branches %." >&2
            exit 1
        fi
        echo "${GREEN}Couverture de branche : $mesure % (seuil : $seuil_branches %)${RESET}"
    fi
fi

if [[ "$ouvrir" -eq 1 ]]; then
    open "$rapport/index.html"
fi