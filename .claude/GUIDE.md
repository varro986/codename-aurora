# Codename Aurora — Guida al sistema `.claude/`

<!-- Questo file è la guida leggibile da un umano che accompagna CLAUDE.md.
     CLAUDE.md contiene istruzioni sintetiche per l'IA.
     Questo file spiega il PERCHÉ di ogni regola, in un linguaggio
     che non richiede un background da sviluppatore per essere compreso.
     
     REGOLA DI ALLINEAMENTO: ogni volta che modifichi un hook, uno skill,
     un command o settings.json, aggiorna questo file nello stesso commit.
     Il coupling-guard hook lo fa rispettare automaticamente. -->

---

## A cosa serve questa guida?

Questo progetto usa **Claude Code** — un assistente IA che vive nel terminale.
Lasciato senza vincoli, un'IA può scrivere codice tecnicamente corretto che però viola
le regole architetturali del progetto, commette per errore una password, o fa push di
codice non testato.

La cartella `.claude/` è il "pannello di controllo" che previene questi errori.
Questa guida spiega ogni componente: cosa fa, quando entra in gioco, e perché esiste.

Se torni su questo progetto dopo una lunga pausa, inizia da qui. C'è tutto.

---

## Il quadro d'insieme

```
Tu scrivi un prompt →
  Claude Code lo riceve →
    Se chiedi a Claude di eseguire un comando shell (es. git commit):
      gli HOOK scattano prima → controllano l'azione → bloccano o lasciano passare
    Se chiedi a Claude di scrivere un file:
      gli HOOK scattano dopo → controllano il risultato → avvertono o lasciano passare
    Prima che Claude agisca su un certo tipo di task (scrivere codice, eseguire CI, ecc.):
      gli SKILL vengono caricati → Claude legge la checklist rilevante
    Puoi avviare workflow strutturati manualmente:
      COMANDI (/audit, /new-feature, /status) →
        Claude segue le istruzioni passo per passo
```

---

## I componenti

```
.claude/
├── CLAUDE.md       ← istruzioni sintetiche per Claude (le legge l'IA)
├── GUIDE.md        ← questo file (lo legge il developer umano)
├── settings.json   ← collega gli hook agli eventi (gli "impianti idraulici")
├── hooks/          ← controlli automatici (script shell)
├── skills/         ← playbook specifici per tipo di task (file Markdown)
└── commands/       ← slash command digitabili nel prompt
```

---

## `settings.json` — il cablaggio degli hook

`settings.json` dice a Claude Code: "quando accade l'evento X, esegui lo script Y".
Non devi modificarlo a meno che tu non aggiunga un nuovo hook.

Cablaggio attuale:
```
PreToolUse  su Bash o PowerShell  →  coupling-guard, verification-guard, secret-scan
PostToolUse su Edit o Write       →  smell-guard
PreCompact                        →  pre-compact
```

**Tipi di evento spiegati:**
- `PreToolUse` — scatta *prima* che lo strumento venga eseguito. Può bloccare l'azione (exit 2 = blocco).
- `PostToolUse` — scatta *dopo* che lo strumento è stato eseguito. Può avvertire ma non può annullare.
- `PreCompact` — scatta prima che Claude Code comprima la conversazione per risparmiare spazio.

**Perché 3 hook scattano su ogni comando Bash?**
Ogni hook legge il comando, decide se è rilevante (es. "è un git commit?"), ed esce
immediatamente (con exit 0 = passa) se non lo è. L'overhead è ~10ms per hook.
Vantaggio: ogni controllo rilevante è garantito a eseguire sempre.

---

## Hook — la rete di sicurezza automatica

Un **hook** è uno script shell che Claude Code esegue automaticamente in momenti specifici.
Non devi mai chiamare gli hook manualmente.

**Come gli hook comunicano con Claude Code:**
| Exit code | Significato |
|---|---|
| `0` | Tutto ok. Continua. |
| `2` | Problema trovato. Blocca l'azione. Mostra il messaggio. |

Ogni hook bloccante può essere aggirato con una variabile d'ambiente:
```bash
SKIP_COUPLING_CHECK=1  git commit ...   # aggira coupling-guard
SKIP_SECRET_SCAN=1     git commit ...   # aggira secret-scan
SKIP_VERIFY_CHECK=1    git push ...     # aggira verification-guard
```
Usa gli override solo quando hai *confermato* che il controllo è un falso positivo.

---

### `coupling-guard.sh`
**Scatta su:** `git commit`

**Controlla cinque cose:**

1. **Dipendenza orizzontale tra moduli** — un file `.csproj` ha un nuovo `<ProjectReference>`.
   Significa che un modulo dipende da un altro. La regola: i moduli possono dipendere solo da
   `CodenameAurora.Core`. Solo `CodenameAurora.App` (la radice di composizione) può referenziare tutti i moduli.
   
   *Perché:* Se i moduli dipendono direttamente l'uno dall'altro, cambiare uno rompe l'altro in
   modi imprevedibili. Le dipendenze scorrono in un'unica direzione — tutto dipende da Core, nient'altro.

2. **Test saltato committato** — un test marcato `[Fact(Skip=...)]` viene committato.
   
   *Perché:* Un test saltato è una promessa non mantenuta. Acceptance criteria che hai detto "fatto"
   ma non hai verificato. I test saltati si accumulano e la suite diventa teatro. Se non riesci
   davvero a sistemarlo ora, apri una GitHub Issue.

3. **Interfaccia Core modificata** — un file `.cs` in `CodenameAurora.Core` viene committato e
   il diff contiene cambiamenti a interfacce o tipi asincroni.
   
   *Perché:* Core è il contratto condiviso tra tutti i moduli. Cambiare un'interfaccia Core senza
   aggiornare tutti gli implementatori rompe la build per chiunque. Il lockstep richiesto:
   aggiorna tutti gli implementatori, esegui i test di architettura, crea un ADR se è strutturale.

4. **`.claude/` GUIDE non sincronizzata** — un file hook/skill/command/settings è stato modificato
   senza che `.claude/GUIDE.md` sia in staging nello stesso commit.
   
   *Perché:* Questa guida è utile solo se rimane accurata. L'hook fa rispettare la co-evoluzione.

5. **`.github/` GUIDE non sincronizzata** — un workflow, un template, CODEOWNERS o dependabot
   è stato modificato senza che `.github/GUIDE.md` sia in staging nello stesso commit.
   
   *Perché:* Lo stesso principio vale per il layer di automazione GitHub. Entrambe le guide
   devono restare allineate con i file che descrivono.

---

### `secret-scan.sh`
**Scatta su:** `git commit`

**Controlla:** credenziali e chiavi API nel contenuto in staging.

Pattern intercettati:
| Pattern | Esempio |
|---|---|
| AWS access key | `AKIA...` |
| Chiave Anthropic | `sk-ant-api03-...` |
| Chiave OpenAI-style | `sk-...` |
| GitHub Personal Access Token | `ghp_...`, `gho_...`, `ghu_...` |
| GitHub fine-grained PAT | `github_pat_...` |
| Chiave PEM privata | `-----BEGIN RSA PRIVATE KEY-----` |
| Password hardcoded | `password = "abc123"` |
| Assegnazione API key | `api_key = "valorelungo"` |
| Connection string con password | `Server=x;Password=y` |

**Perché esiste:** Un segreto committato in un repository git è esposto permanentemente, anche
se lo elimini nel commit successivo. Git conserva ogni versione di ogni file per sempre.
L'unico modo per rimediare è: eliminare la credenziale (invalidarla), riscrivere la storia git,
e notificare chiunque possa aver clonato il repo. Un hook che gira per 50ms per commit è un
ottimo investimento.

**Se è un falso positivo:** aggiungi `SKIP_SECRET_SCAN=1` prima del comando, documenta perché.

---

### `verification-guard.sh`
**Scatta su:** `git push`

**Controlla:** I test sono stati eseguiti *dopo* l'ultima modifica a un file `.cs`?

Trova il file `.trx` più recente in `TestResults/` (il log dei risultati dei test) e confronta
il suo timestamp con il tempo di modifica di ogni file `.cs` nei commit in uscita.
Se un file `.cs` è più recente dell'ultimo test run → bloccato.

**Perché esiste:** "L'ho compilato" non è la stessa cosa di "l'ho testato". `dotnet build`
compila il codice sorgente ma NON compila né esegue i progetti di test. Questo ha causato
incidenti reali in produzione. L'hook impone: devi eseguire `dotnet test` prima di fare push.

**Override:** `SKIP_VERIFY_CHECK=1 git push ...`
**Comando test corretto:** `dotnet test --configuration Release --results-directory TestResults/ --logger trx`

---

### `smell-guard.sh`
**Scatta su:** dopo che Claude scrive o modifica un file `.cs` o `.feature`

**Controlla le violazioni delle regole di stile nelle righe appena scritte da Claude:**

| Regola | Cosa intercetta | Perché |
|---|---|---|
| `NO_WHAT_COMMENT` | `// questo metodo fa X` — commenti che descrivono cosa fa il codice | Il codice stesso descrive già cosa fa. I commenti devono spiegare il *perché* di una scelta non ovvia. |
| `NO_BLOCKING_ASYNC` | `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` | Blocca il thread corrente mentre aspetta lavoro asincrono. In una UI app congela l'interfaccia. Usa sempre `await`. |
| `NO_ASYNC_VOID` | `async void DoSomething()` | Se un metodo `async void` lancia un'eccezione, non può essere catturata. L'app crasha silenziosamente. Usa `async Task`. |
| `NO_EMPTY_CATCH` | `catch { }` — un catch block vuoto | Inghiotte silenziosamente le eccezioni. I bug diventano invisibili. Come minimo, logga l'eccezione. |
| `NO_DISPATCHER_INVOKE` | `.Dispatcher.Invoke(` | Dispatch sincrono sul thread UI. Usa `InvokeAsync` per evitare deadlock. |
| `NO_CONSOLE_WRITELINE` | `Console.WriteLine(` nel codice di produzione | Usa il logger strutturato (`ILogger<T>`). L'output su Console è non strutturato e si perde in produzione. |
| `FORBIDDEN_COMMENT` | `FIXME:`, `STOPSHIP:` | Questi significano "non spedire così". Se non è sistemato prima del commit, apri una GitHub Issue. |
| `NO_NULLABLE_SUPPRESSION` | `qualcosa!.Proprietà` senza un commento `// perché` | L'operatore `!` dice al compilatore "so che questo non è null" — senza una ragione è una bugia che aspetta di diventare un NullReferenceException. |
| `MISSING_NULLABLE` | `#nullable enable` mancante in cima a un nuovo file `.cs` | Le reference types nullable sono una funzionalità di sicurezza di C# 8+. Senza di essa, il compilatore non avvertirà sui bug legati ai null. Obbligatorio su ogni file. |
| `NO_HARDCODED_SECRET` | `password = "..."`, `"Server=..."` nel codice di produzione | Vedi secret-scan per la spiegazione completa. |

**Perché al momento della scrittura, non al commit?**
Un problema trovato mentre Claude scrive il file viene sistemato in secondi — Claude riscrive
semplicemente la riga. Un problema trovato in CI review richiede un round-trip completo:
CI fallisce, leggi il report, sistemi, fai push di nuovo. Lo smell-guard sposta il costo
della qualità il più presto possibile.

---

### `pre-compact.sh`
**Scatta su:** prima che Claude Code comprima la conversazione

**Cosa fa:** Produce un sommario strutturato dello stato git attuale che viene iniettato
nella conversazione compressa. Senza questo, dopo la compressione Claude dimentica: su quale
branch siamo, quali sono stati gli ultimi 5 commit, se i test hanno girato, quali file hanno
modifiche non committate.

**Perché è importante:** Claude Code comprime automaticamente le conversazioni lunghe per
stare entro il suo context window. La compressione riassume la conversazione ma può perdere
dello stato tecnico. Questo hook garantisce che lo stato critico venga sempre preservato.

---

## Skill — playbook specifici per tipo di task

Uno **skill** è un file Markdown che Claude carica prima di affrontare un tipo specifico di lavoro.
Pensa a lui come a una checklist + guida decisionale che Claude consulta prima di agire.

Gli skill **non sono automatici** — Claude li carica basandosi sulla routing table in `CLAUDE.md`.
Caricare uno skill è obbligatorio quando la routing table corrisponde.

| Skill | Caricato quando | Cosa copre |
|---|---|---|
| `architecture` | Prima di toccare qualsiasi file `.cs` | Regole sui layer, isolamento dei moduli, convenzioni sulle interfacce, come aggiungere un modulo |
| `code-quality` | Dopo `architecture`, prima di scrivere codice | SOLID, limiti di dimensione dei file, criteri di completezza, pattern di decomposizione |
| `feature-spec` | Leggendo una Issue GitHub, scrivendo test o file `.feature` | Come tradurre una Issue in test xUnit e scenari Gherkin |
| `ci-workflow` | Prima di build, test, push o PR | Comandi `dotnet` corretti, cosa significa "build verde", step della CI pipeline |
| `investigate` | Scope della feature incerto, blast radius non chiaro | Come mappare tutti i layer che una modifica tocca prima di scrivere qualcosa |
| `pre-release-validation` | Prima di qualsiasi push o PR | Checklist a tier (T1 veloce → T3 completo) da eseguire in locale prima del push |

**Perché skill separati invece di un unico grande CLAUDE.md?**
Un file di istruzioni da 500 righe verrebbe caricato in ogni conversazione anche quando la
maggior parte è irrilevante. Gli skill mantengono il context lean: scrivere un test carica
`feature-spec`, non l'intero manuale delle regole.

---

## Comandi — slash command digitabili

Digitali nel prompt di Claude Code per avviare un workflow strutturato.

| Comando | Cosa fa |
|---|---|
| `/audit` | Audit completo del repository: ogni file controllato rispetto alle regole di AGENTS.md, code smell (AS-1..AS-6), isolamento dei moduli. Riporta tutte le violazioni con file:riga e gravità. |
| `/new-feature N` | Avvia il workflow per una nuova feature per la GitHub Issue N: carica lo skill → legge la Issue → fa checkout del branch → legge lo stub di test → propone un piano. Attende conferma prima di scrivere codice. |
| `/status` | Mostra lo stato attuale: branch, file staged/unstaged, ultimi commit, ultimo test run. |

---

## Come far evolvere questo sistema

Quando aggiungi o modifichi qualsiasi componente in `.claude/`:

1. **Aggiungi un hook** → aggiorna la sezione "Hook" con la stessa struttura:
   su cosa scatta, cosa controlla, perché esiste, come aggirarlo.

2. **Aggiungi uno skill** → aggiorna la tabella degli skill con il trigger e cosa copre.

3. **Aggiungi un comando** → aggiorna la tabella dei comandi.

4. **Modifichi settings.json** → aggiorna la sezione "settings.json".

5. **Il coupling-guard lo fa rispettare** — se committa un file di sistema `.claude/`
   senza aggiornare questa guida, il commit viene bloccato.

---

## Glossario

| Termine | Spiegazione semplice |
|---|---|
| **Hook** | Uno script shell che gira automaticamente in un momento specifico (prima/dopo un'azione). Può bloccare l'azione se qualcosa sembra sbagliato. |
| **Skill** | Una checklist Markdown che Claude carica prima di un tipo specifico di task. |
| **Slash command** | Un workflow strutturato che avvii digitando `/nome` nel prompt. |
| **File staged** | Un file che hai detto a git "includi questo nel prossimo commit" — ma non ancora committato. |
| **File `.trx`** | Il file di output di `dotnet test`. Contiene i risultati dei test e i timestamp. |
| **Composition root** | L'unico posto nell'app dove tutti i moduli vengono collegati insieme. In questo progetto: `CodenameAurora.App`. |
| **Core** | Il layer del contratto condiviso. Definisce le interfacce che tutti i moduli implementano. Non ha dipendenze proprie. |
| **Modulo** | Un'unità autonoma di funzionalità (OCR, Translation, UI, Admin). Ogni modulo dipende solo da Core. |
| **Context window** | La quantità massima di testo che l'IA può "tenere in mente" in una conversazione. Quando si riempie, Claude Code comprime automaticamente. |
