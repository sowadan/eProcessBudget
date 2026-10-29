// ═══════════════════════════════════════════════════════════════════════════════
//  Guide d'utilisation interactif (visites guidées), construit sur driver.js.
//
//  Le CONTENU des visites est écrit côté serveur (Features/Aide/GuidesApplication.cs) ;
//  ce script ne fait que les jouer dans la page :
//    - il résout l'élément de chaque étape au moment du lancement ;
//    - il SAUTE les étapes dont l'élément est absent ou masqué (bouton réservé à un droit,
//      section du menu repliée, tableau vide...) : la visite s'adapte à ce que
//      l'utilisateur voit réellement ;
//    - il écarte les étapes qui retombent sur un élément déjà présenté.
//
//  Désignation d'un élément (champ « element » d'une étape) :
//    null               → bulle centrée, sans élément ;
//    "sélecteur CSS"    → premier élément visible correspondant ;
//    "carte:sélecteur"  → la carte (.carte) qui contient le premier élément trouvé ;
//    "groupe:sélecteur" → le parent du premier élément trouvé (une rangée de tuiles...).
//
//  Appels depuis C# :
//    epbGuide.lancer(guide)                       → joue une visite { titre, etapes[] }
//    epbGuide.premiereVisite(cle, guide)          → la joue une seule fois par navigateur
//    epbGuide.arreter()                           → interrompt la visite en cours
// ═══════════════════════════════════════════════════════════════════════════════
window.epbGuide = (() => {
    const COTES = { haut: 'top', droite: 'right', bas: 'bottom', gauche: 'left' };
    const ATTENTE_MAX_MS = 4000;
    const PREFIXE_CLE = 'epb.guide.vu.';

    let enCours = null;

    const echapper = (texte) => {
        const div = document.createElement('div');
        div.textContent = texte ?? '';
        return div.innerHTML;
    };

    const estVisible = (el) => !!el && el.getClientRects().length > 0
        && getComputedStyle(el).visibility !== 'hidden';

    const premierVisible = (selecteur) => {
        try {
            return Array.from(document.querySelectorAll(selecteur)).find(estVisible) ?? null;
        } catch {
            return null;   // sélecteur invalide : l'étape est sautée
        }
    };

    const resoudre = (designation) => {
        if (!designation) return null;
        const [, mode, selecteur] = designation.match(/^(carte|groupe):(.*)$/) ?? [null, null, designation];
        const el = premierVisible(selecteur);
        if (!el) return undefined;
        if (mode === 'carte') return el.closest('.carte') ?? el;
        if (mode === 'groupe') return el.parentElement ?? el;
        return el;
    };

    const pause = (ms) => new Promise(r => setTimeout(r, ms));

    /** Attend que la page ait rendu son en-tête (données chargées après la navigation). */
    const attendrePage = async () => {
        const debut = Date.now();
        while (!premierVisible('[data-guide="page-entete"]') && Date.now() - debut < ATTENTE_MAX_MS) {
            await pause(100);
        }
        await pause(250);   // laisse aux tableaux et graphiques le temps de s'afficher
    };

    const construireEtapes = (guide) => {
        const dejaVus = new Set();
        const titrePage = document.querySelector('main h1')?.textContent?.trim();
        const etapes = [];

        for (const etape of guide.etapes ?? []) {
            const el = resoudre(etape.element);
            if (el === undefined) continue;            // élément attendu mais absent : on saute
            if (el && dejaVus.has(el)) continue;       // déjà présenté par une étape précédente
            if (el) dejaVus.add(el);

            etapes.push({
                element: el ?? undefined,
                popover: {
                    title: echapper(etape.titre || titrePage || guide.titre),
                    description: etape.texte,           // texte rédigé côté serveur (balises simples admises)
                    side: COTES[etape.cote] ?? undefined,
                    align: 'start'
                }
            });
        }
        return etapes;
    };

    const lancer = async (guide) => {
        if (!window.driver?.js?.driver || !guide) return false;
        arreter();
        await attendrePage();

        const etapes = construireEtapes(guide);
        if (etapes.length === 0) return false;

        enCours = window.driver.js.driver({
            steps: etapes,
            showProgress: etapes.length > 1,
            progressText: '{{current}} / {{total}}',
            nextBtnText: 'Suivant',
            prevBtnText: 'Précédent',
            doneBtnText: 'Terminer',
            allowClose: true,
            smoothScroll: true,
            overlayOpacity: 0.55,
            stagePadding: 6,
            stageRadius: 8,
            popoverClass: 'epb-guide',
            onDestroyed: () => { enCours = null; }
        });
        enCours.drive();

        // La demande de visite passée dans l'adresse (?visite=...) est consommée.
        const url = new URL(window.location.href);
        if (url.searchParams.has('visite')) {
            url.searchParams.delete('visite');
            history.replaceState(history.state, '', url.pathname + url.search + url.hash);
        }
        return true;
    };

    const arreter = () => {
        if (enCours?.isActive()) enCours.destroy();
        enCours = null;
    };

    const lire = (cle) => { try { return localStorage.getItem(PREFIXE_CLE + cle); } catch { return null; } };
    const ecrire = (cle) => { try { localStorage.setItem(PREFIXE_CLE + cle, new Date().toISOString()); } catch { /* stockage bloqué */ } };

    return {
        lancer,
        arreter,

        /** Joue la visite si ce navigateur ne l'a jamais montrée pour cette clé (utilisateur). */
        async premiereVisite(cle, guide) {
            if (lire(cle)) return false;
            const lancee = await lancer(guide);
            if (lancee) ecrire(cle);
            return lancee;
        }
    };
})();
