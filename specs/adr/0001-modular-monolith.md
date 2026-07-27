# ADR-001: Adozione del Pattern Modular Monolith

* **Stato:** Approvato
* **Data:** 2026-07-27
* **Autore:** Architect

---

## 1. Contesto e Problema
Code Name Aurora necessita di un'architettura rigorosa, manutenibile e modulare che separi nettamente le responsabilità di OCR, Traduzione, UI e Amministrazione. Trattandosi di un tool desktop gestito da un singolo sviluppatore/small team, l'adozione di un'architettura a Microservizi introdurrebbe un'eccessiva complessità di rete, deploy e manutenzione.

## 2. Opzioni Valutate
1. **Monolito Tradizionale (Spaghetti Code):**
   * *Pro:* Velocità iniziale di sviluppo.
   * *Contro:* Alto accoppiamento, dipendenze incrociate, difficile da testare e da evolvere.
2. **Microservizi:**
   * *Pro:* Isolamento totale e deployment indipendente.
   * *Contro:* Complessità operativa ingiustificata, overhead di rete e gestione IPC complessa.
3. **Modular Monolith (.NET 8.0):**
   * *Pro:* Isolamento logico dei moduli via progetti C#, contratti chiari via `Core`, singola pipeline di build/deploy.
   * *Contro:* Richiede una governance rigorosa via NetArchTest per evitare accoppiamenti indebiti.

## 3. Decisione
Si adotta il pattern **Modular Monolith**. La solution sarà composta da 5 progetti .NET 8.0 separati. Tutti i moduli dipendono unicamente dal modulo `Core`. Non è ammessa alcuna dipendenza diretta tra i moduli operativi (`OCR`, `Translation`, `UI`, `Admin`).

## 4. Conseguenze
* **Impatti Positivi:** Manutenibilità elevata, confini di dominio chiari, facili test unitari/architetturali.
* **Rischi e Trade-off:** Rischio di dipendenze cicliche se non filtrate in CI da *NetArchTest*.

## 5. Riferimenti
* Specifica Architetturale: `specs/archi.md`