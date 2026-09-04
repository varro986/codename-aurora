# Codename Aurora — Guida al sistema `.github/`

<!-- Questo file è la guida leggibile da un umano a come GitHub è configurato
     per questo progetto: cosa fa ogni workflow, quando gira, e perché esiste.
     
     REGOLA DI ALLINEAMENTO: ogni volta che modifichi un workflow, un template,
     CODEOWNERS, dependabot.yml o uno script di setup, aggiorna questo file
     nello stesso commit. Il coupling-guard hook lo fa rispettare automaticamente. -->

---

## Branch strategy — il flusso da Issue a main

**Modello:** GitHub Flow. `main` è sempre stabile. Ogni modifica passa obbligatoriamente per una PR.
Il processo è identico con 1 developer o con N.

### Tipi di branch

| Tipo | Nome | Chi lo crea |
|---|---|---|
| Default | `main` | Sempre presente, protetto |
| Feature | `feature/issue-N-slug` | Bootstrap workflow (automatico all'etichetta `status: active`) |
| Hotfix | `hotfix/issue-N-slug` | Checkout manuale da `main` per bug urgenti su codice già rilasciato |

### Il flusso passo per passo

```
1. Etichetti la Issue con "status: active"
      ↓
2. Bootstrap workflow crea automaticamente feature/issue-N-slug
   + scrive lo stub del test + posta un commento sulla Issue
      ↓
3. git fetch --all && git checkout feature/issue-N-slug
      ↓
4. Implementi: togli [Fact(Skip)], scrivi i test prima, poi il codice
      ↓
5. git push origin feature/issue-N-slug
      ↓
6. Apri PR verso main (GitHub mostra un banner con il pulsante)
      ↓
7. CI gira: build + test + lint devono passare
      ↓
8. Fai review della tua PR (leggi il diff, compila il template)
      ↓
9. Mergi con SQUASH (1 feature = 1 commit su main)
      ↓
10. Branch eliminato dopo il merge
```

### Merge con squash — perché?

Un merge con squash trasforma tutti i commit del branch (anche quelli intermedi
"fix typo", "wip", "prova") in un singolo commit su `main`.
Risultato: la storia di `main` è leggibile — un commit = una feature o un bug fix.
La storia dettagliata resta sul branch finché non viene eliminato.

### Release

Tag su `main` con il formato semantico `v0.1.0`, `v1.0.0`, ecc.:
```bash
git tag v0.1.0
git push origin v0.1.0
```
Il job `publish` della CI scatta automaticamente su `refs/tags/v*`.

### Reviewer richiesti — auto-calibrati

| Situazione | Approval richieste | Come si attiva |
|---|---|---|
| Solo developer | 0 (self-review formale: compila il template, leggi il diff, poi mergi) | Default |
| Team (2+ persone) | 1 da un altro membro | Riesegui `setup-repo.sh` dopo aver aggiunto la seconda persona |

La calibrazione è automatica: `setup-repo.sh` conta i membri del team architects e imposta il numero corretto. Non devi cambiare nulla manualmente.

### Mai push diretto su main

Non fare mai `git push origin qualcosa:main`.
Non fare mai `git push origin main`.
L'unica eccezione è un tag di release: `git push origin v1.0.0`.

---

## A cosa serve questa guida?

GitHub non è solo un posto dove salvare il codice. Per questo progetto è anche una
fabbrica automatizzata: esegue controlli di qualità su ogni push, crea branch da
issue automaticamente, e impedisce al codice difettoso di raggiungere `main`.

Questa guida spiega ogni file in `.github/` — cosa fa, quando gira, e perché esiste.
Non è richiesta esperienza con GitHub Actions.

---

## La cartella `.github/` — cosa c'è dentro

```
.github/
├── GUIDE.md                    ← questo file
├── CODEOWNERS                  ← chi deve fare review di quali file
├── dependabot.yml              ← PR automatiche di aggiornamento dipendenze
├── PULL_REQUEST_TEMPLATE.md    ← checklist pre-compilata in ogni PR
├── setup-labels.sh             ← script da eseguire una volta sola: crea le label GitHub
├── setup-repo.sh               ← script da eseguire una volta sola: configura il repo
├── ISSUE_TEMPLATE/
│   ├── config.yml              ← disabilita le issue senza template; imposta le opzioni
│   ├── bug_report.yml          ← modulo strutturato per segnalare bug
│   └── feature.yml             ← modulo strutturato per richiedere funzionalità (User Story)
└── workflows/
    ├── ci.yml                  ← pipeline CI (gira su ogni push e ogni PR)
    └── bootstrap-feature.yml   ← automazione: Issue → branch + stub del test
```

---

## Workflows — la pipeline automatizzata

Un **workflow** è un insieme di step automatizzati che GitHub esegue su un server (un "runner")
ogni volta che accade un evento specifico (un push, una PR, una issue etichettata, ecc.).
I workflow non si eseguono manualmente — accadono sull'infrastruttura di GitHub, non sulla tua macchina.

---

### `ci.yml` — la pipeline CI

**Trigger:** ogni push su `main`, ogni pull request che punta a `main`.

CI sta per **Continuous Integration** — "ogni volta che il codice cambia, controllalo automaticamente".
Questa pipeline ha 5 job indipendenti che girano in parallelo (tranne `test`, che aspetta `build`).

#### Job: `build`
| Dettaglio | Valore |
|---|---|
| Runner | `windows-latest` |
| Step | checkout → setup .NET 8 → `dotnet restore` → `dotnet build --configuration Release` |

`dotnet restore` scarica tutti i pacchetti NuGet (le librerie esterne usate dal progetto).
`dotnet build --configuration Release` compila il codice sorgente in modalità produzione.

**Perché Windows?** L'app usa WPF (Windows Presentation Foundation) e il motore OCR di Windows.
Esistono solo su Windows. Il runner di build deve corrispondere alla piattaforma target.

**Cosa intercetta:** errori di compilazione, pacchetti mancanti, riferimenti rotti tra moduli.

---

#### Job: `test`
| Dettaglio | Valore |
|---|---|
| Runner | `windows-latest` |
| Dipende da | `build` deve passare prima |
| Step | checkout → setup .NET → restore → `dotnet test --configuration Release --logger trx` → upload risultati |

`dotnet test` compila ed esegue ogni progetto di test. `--logger trx` salva i risultati in un file `.trx`.
Lo step `upload-artifact` salva quei risultati così puoi scaricarli e ispezionarli.

**Perché `test` dipende da `build`?**
Se il codice non compila, i test fallirebbero con un errore di build confuso invece di un
vero fallimento di test. La dipendenza `needs: build` ti dà un segnale più chiaro.

**Cosa intercetta:** test unitari falliti, test di architettura falliti (tag `Category=Architecture`).

---

#### Job: `rules`
| Dettaglio | Valore |
|---|---|
| Runner | `ubuntu-latest` |
| Gira solo su | pull request (non su push diretti) |
| Skip con | label PR `rules-exempt` |

Esegue due script sul diff della PR:
- `bash scripts/ci/smell-check.sh` — **bloccante**: stesse regole dello `smell-guard` hook locale.
  Se una PR introduce un catch block vuoto o una chiamata `.Result`, questo job fallisce e la PR è bloccata.
- `bash scripts/ci/coupling-check.sh` — **advisory**: controlla dipendenze orizzontali tra moduli.
  Questo job passa sempre (exit 0) ma stampa avvertimenti nel log.

**Perché su Ubuntu quando l'app gira su Windows?**
Questi script leggono solo file di testo (il git diff). Non compilano né eseguono l'app.
I runner Ubuntu sono più veloci ed economici di quelli Windows. Strumento giusto per il lavoro giusto.

**Perché solo su PR, non su push diretti?**
I push diretti su `main` vengono da un flusso controllato (hook locali + pre-release validation già girate).
Le PR vengono da contributor o da branch automatici — un gate CI aggiuntivo è la rete di sicurezza.

---

#### Job: `lint`
| Dettaglio | Valore |
|---|---|
| Runner | `ubuntu-latest` |
| Step | checkout → setup .NET → installa CSharpier → `dotnet csharpier --check .` |

CSharpier è un **formatter di codice opinionato** per C#. "Opinionato" significa che ha un
unico modo corretto di formattare ogni riga di codice, e lo fa rispettare senza opzioni di configurazione.

La modalità `--check` non modifica i file — esce con codice 1 se un file avrebbe bisogno di riformattazione.

**Perché:** Le discussioni sulla formattazione ("spazi o tab?", "graffa sulla stessa riga?") sprecano
attenzione durante la code review che dovrebbe andare su logica e correttezza. CSharpier elimina
completamente il dibattito: un formato, sempre. Se il tuo file non è formattato, la CI fallisce.

**Nota:** i tool .NET globali vengono installati in `$HOME/.dotnet/tools`, che non è nel PATH di default
su GitHub Actions. Lo step di installazione esporta esplicitamente il percorso in `$GITHUB_PATH` prima
di invocare il formatter — omettere questo passaggio causa un "command not found" fuorviante.

---

#### Job: `publish`
| Dettaglio | Valore |
|---|---|
| Runner | `windows-latest` |
| Gira solo su | tag git che iniziano con `v` (es. `v1.0.0`) |
| Dipende da | `build` e `test` devono passare |

Compila l'applicazione in modalità Release e carica l'output come artifact di GitHub Actions.
È lo step "pacchettizza per la distribuzione" — gira solo quando tagghi una release, non ad ogni commit.

**Nota:** Un placeholder `[PROJECT-SPECIFIC]` segna il punto dove aggiungere il packaging MSIX/installer.

---

### `bootstrap-feature.yml` — Issue → branch + stub del test

**Trigger:** una Issue viene etichettata con `status: active`.

Questo è uno dei meccanismi più potenti del workflow. Quando etichetti una Issue come attiva,
GitHub automaticamente:
1. Crea un branch chiamato `feature/issue-N-slug-del-titolo`
2. Scrive uno stub di test in `tests/CodenameAurora.Tests.Unit/IssueNTests.cs`
3. Committa e fa push del branch
4. Posta un commento sulla Issue con il nome del branch e le istruzioni per il checkout

**Lo stub del test appare così:**
```csharp
#nullable enable
using Xunit;

namespace CodenameAurora.Tests.Unit;

public sealed class Issue42Tests
{
    [Fact(Skip = "Stub — rimuovi Skip e implementa rispetto ai criteri di accettazione della Issue #42")]
    [Trait("US", "US-42")]
    public void Nome_Scenario_Qui()
    {
        // Arrange / Act / Assert
        throw new NotImplementedException();
    }
}
```

**Perché inizia con `[Fact(Skip)]`?**
Lo stub esiste per darti un punto di partenza. L'attributo `Skip` significa che il test non gira
(e quindi non fallisce) finché non sei pronto a implementare. Il tuo primo task quando prendi il
branch: rinomina il test, rimuovi `Skip`, e riempi le assertion prima di scrivere qualsiasi codice
di produzione. Questo è lo **sviluppo test-first** — definisci com'è il "fatto" prima di scrivere
il codice che lo rende vero.

**Perché automatizzare questo?**
Convenzioni di naming per i branch, stub dei test, e collegamento alla Issue sono task meccanici
che consumano attenzione senza aggiungere valore. L'automazione gestisce il cablaggio così il
developer parte sempre da un punto di partenza pulito e strutturato correttamente.

**Idempotente:** se il branch esiste già, il workflow salta la creazione e non fa nulla di dannoso.

---

## Template delle Issue — bug report e feature request strutturati

GitHub permette di definire form che i contributor (e tu) compilano quando aprono una Issue.
I dati strutturati sono più utili del testo libero: forzano chi segnala a fornire le informazioni
necessarie per agire sulla segnalazione.

### `bug_report.yml`
Un modulo strutturato con campi obbligatori: descrizione, passi per riprodurre, comportamento
atteso vs. effettivo, versione di Aurora, versione di Windows. Opzionale: log e screenshot.

Il suggerimento sul percorso dei log (`%APPDATA%\CodenameAurora\logs\`) dice esattamente dove
trovare i file diagnostici — eliminando il rimbalzo "non so cosa allegare".

### `feature.yml`
Un modulo per le User Story. Cattura: la richiesta di funzionalità come storia "Come... voglio...
così che...", i criteri di accettazione (la lista delle condizioni che rendono la feature "fatta"),
e eventuali vincoli di implementazione C#.

**Perché le User Story?**
Una User Story forza chi richiede a definire il *valore* della funzionalità ("così che io possa..."),
non solo l'implementazione tecnica. Questo mantiene il progetto concentrato sui risultati, non sulle feature.

### `config.yml`
Disabilita le issue senza template — non puoi aprire una Issue senza usare un template.
Questo garantisce che ogni Issue abbia il minimo delle informazioni necessarie per agire.

---

## `PULL_REQUEST_TEMPLATE.md` — la checklist della PR

Ogni volta che si apre una PR, questo template viene pre-compilato nella casella della descrizione.
È una checklist e un modulo strutturato in uno.

Sezioni:
- **Summary**: cosa è cambiato e perché (non un elenco di file — la CI lo mostra già)
- **Behavior changes**: ogni differenza visibile all'utente dopo questa PR
- **Hot paths**: questa PR aggiunge I/O o lavoro bloccante su un percorso critico?
- **Testing evidence**: cosa hai eseguito e cosa hai osservato
- **Spec sync**: hai aggiornato i file di spec per riflettere quanto costruito?
- **AI assistance**: trasparenza sul coinvolgimento dell'IA (richiesta dalla policy del progetto)
- **Checklist**: build, test, formato, nessun segreto, spec sync, review personale

**Perché "AI assistance" come campo obbligatorio?**
Questo progetto usa Claude Code intenzionalmente e in modo trasparente. Il campo normalizza
la dichiarazione — ogni PR dichiara onestamente quanto IA è stata coinvolta, evitando sia il
diniego che l'eccessiva attribuzione.

---

## `CODEOWNERS` — assegnazione automatica della review

CODEOWNERS è una funzionalità di GitHub: quando una PR tocca certi file o directory,
i proprietari elencati vengono automaticamente aggiunti come reviewer obbligatori.

Attualmente: `@varro986` possiede tutto (`*`). Con la crescita del progetto, si possono aggiungere
proprietari specifici per `src/CodenameAurora.Admin/`, `specs/`, ecc.

**Perché:** Garantisce che nessuna modifica a un'area critica venga mergiata senza che la persona
giusta l'abbia vista.

---

## `dependabot.yml` — aggiornamenti automatici delle dipendenze

Dependabot è un bot GitHub che apre automaticamente PR per aggiornare le dipendenze
(pacchetti NuGet e versioni di GitHub Actions) con cadenza regolare.

Configurazione attuale:
- NuGet: settimanale, massimo 5 PR aperte contemporaneamente
- GitHub Actions: settimanale, massimo 3 PR aperte contemporaneamente

**Perché:** Le dipendenze obsolete accumulano vulnerabilità di sicurezza silenziosamente.
Dependabot le surfaca come PR — puoi revisionare, testare e mergiare al tuo ritmo,
invece di scoprire un anno di drift accumulato tutto in una volta.

---

## `setup-labels.sh` e `setup-repo.sh` — script di setup una tantum

Questi script si eseguono **una volta sola** quando si configura il repository GitHub per la prima volta.
Non fanno parte della pipeline CI.

- `setup-labels.sh`: crea tutte le label GitHub usate nel workflow
  (es. `status: active`, `bug`, `rules-exempt`). Esegui con `bash .github/setup-labels.sh`.
- `setup-repo.sh`: configura le impostazioni del repository (regole di branch protection, strategie di merge, ecc.).
  Esegui con `bash .github/setup-repo.sh`.

**Non devi rieseguirli** a meno che tu non stia riconfigurando il repo da zero.

---

## Come far evolvere questo sistema

Quando modifichi qualsiasi file in `.github/`:

1. Aggiorna la sezione rilevante in questa guida.
2. Spiega COSA è cambiato e PERCHÉ.
3. Committa la modifica e questa guida nello stesso commit.
4. Il coupling-guard hook lo fa rispettare — un file `.github/` staged senza `GUIDE.md` blocca il commit.

Se aggiungi un nuovo workflow:
- Aggiungi una sezione sotto "Workflows" con la stessa struttura: trigger, job, cosa fa ogni job, perché.

Se aggiungi un nuovo template di Issue:
- Aggiungi un punto sotto "Template delle Issue" spiegando cosa cattura e perché i campi contano.
