// ═══════════════════════════════════════════════════════════════════════════════
//  Session : expiration après inactivité (cahier technique § 2.6).
//
//  Le circuit Blazor (SignalR) ne renouvelle pas le cookie de session. Ce script :
//    - signale l'activité de l'utilisateur au serveur (au plus une fois par minute),
//      ce qui renouvelle l'expiration glissante du cookie ;
//    - ferme la session (formulaire de déconnexion) après la durée d'inactivité
//      paramétrée pour l'organisme.
//
//  Démarré par MainLayout : window.epbSession.demarrer(dureeMinutes).
// ═══════════════════════════════════════════════════════════════════════════════
window.epbSession = (() => {
    const EVENEMENTS = ['mousedown', 'keydown', 'wheel', 'touchstart', 'scroll'];
    const PERIODE_CONTROLE_MS = 30_000;
    const INTERVALLE_SIGNAL_MS = 60_000;

    let dureeMs = 30 * 60_000;
    let derniereActivite = Date.now();
    let dernierSignal = Date.now();
    let minuterie = null;

    const noterActivite = () => { derniereActivite = Date.now(); };

    const fermer = () => {
        const formulaire = document.getElementById('form-deconnexion');
        if (!formulaire) { window.location.href = '/compte/connexion?motif=inactivite'; return; }
        formulaire.querySelector('input[name="motif"]').value = 'inactivite';
        formulaire.submit();
    };

    const signaler = async () => {
        dernierSignal = Date.now();
        try {
            const reponse = await fetch('/compte/session', { credentials: 'same-origin', cache: 'no-store' });
            // Session refusée par le serveur : redirigé vers la connexion.
            if (reponse.redirected || reponse.status === 401) {
                window.location.href = '/compte/connexion?motif=expiree';
            }
        } catch {
            // Réseau indisponible : on réessaiera au prochain contrôle.
        }
    };

    const controler = () => {
        const maintenant = Date.now();
        if (maintenant - derniereActivite >= dureeMs) { fermer(); return; }
        if (derniereActivite > dernierSignal && maintenant - dernierSignal >= INTERVALLE_SIGNAL_MS) signaler();
    };

    return {
        demarrer(dureeMinutes) {
            dureeMs = Math.max(1, dureeMinutes) * 60_000;
            derniereActivite = dernierSignal = Date.now();
            if (minuterie) return;   // déjà démarré (navigation interne)
            EVENEMENTS.forEach(e => document.addEventListener(e, noterActivite, { passive: true }));
            minuterie = setInterval(controler, PERIODE_CONTROLE_MS);
        }
    };
})();
