# Slot Dogs — Paylines & Regras de Ganho

Grade: **5 colunas × 3 linhas** (col 1–5 da esquerda; row 0 = topo, row 2 = base)  
Linhas de pagamento: **15**

As linhas pagam com **3, 4 ou 5 símbolos iguais consecutivos da esquerda para a direita**.  
O **Wild** (Patinha Dourada) substitui qualquer símbolo normal em qualquer linha, mas **só aparece nos rolos 2, 3 e 4**.

---

## Paytable (× betPerLine)

| Símbolo          | ID | 3 símbolos | 4 símbolos | 5 símbolos |
|------------------|----|-----------|-----------|-----------|
| Husky Siberiano  | 0  | 15×       | 75×       | 500×      |
| Golden Retriever | 1  | 8×        | 40×       | 200×      |
| Shiba Inu        | 2  | 6×        | 30×       | 150×      |
| Pug              | 3  | 4×        | 20×       | 80×       |
| Beagle           | 4  | 3×        | 15×       | 60×       |
| Dachshund        | 5  | 2×        | 10×       | 40×       |
| Wild             | 6  | —         | —         | —         |
| Scatter          | 7  | especial  | especial  | especial  |

> Uma linha composta apenas de Wilds paga como Husky (maior símbolo).

---

## Scatter — Ossinho (ID 7)

Paga **em qualquer posição** do grid (não precisa estar em payline).

| Scatters no grid | Multiplicador        | Free Spins |
|-----------------|----------------------|------------|
| 3               | 2× aposta total      | +8         |
| 4               | 5× aposta total      | +8         |
| 5               | 20× aposta total     | +8         |

> Aposta total = `betPerLine × 15` (número de paylines).

---

## As 15 Paylines

Legenda: `■` = célula ativa | `·` = inativa

```
     C1   C2   C3   C4   C5
     col0 col1 col2 col3 col4
```

---

### Linha 1 — "Linha do meio"  `path: [1,1,1,1,1]`

```
  ·    ·    ·    ·    ·    ← row 0 (topo)
  ■    ■    ■    ■    ■    ← row 1 (meio)
  ·    ·    ·    ·    ·    ← row 2 (base)
```

---

### Linha 2 — "Linha de cima"  `path: [0,0,0,0,0]`

```
  ■    ■    ■    ■    ■    ← row 0
  ·    ·    ·    ·    ·    ← row 1
  ·    ·    ·    ·    ·    ← row 2
```

---

### Linha 3 — "Linha de baixo"  `path: [2,2,2,2,2]`

```
  ·    ·    ·    ·    ·    ← row 0
  ·    ·    ·    ·    ·    ← row 1
  ■    ■    ■    ■    ■    ← row 2
```

---

### Linha 4 — "V invertido"  `path: [0,1,2,1,0]`

```
  ■    ·    ·    ·    ■    ← row 0
  ·    ■    ·    ■    ·    ← row 1
  ·    ·    ■    ·    ·    ← row 2
```

---

### Linha 5 — "V normal"  `path: [2,1,0,1,2]`

```
  ·    ·    ■    ·    ·    ← row 0
  ·    ■    ·    ■    ·    ← row 1
  ■    ·    ·    ·    ■    ← row 2
```

---

### Linha 6 — "Diagonal ↘"  `path: [0,0,1,2,2]`

```
  ■    ■    ·    ·    ·    ← row 0
  ·    ·    ■    ·    ·    ← row 1
  ·    ·    ·    ■    ■    ← row 2
```

---

### Linha 7 — "Diagonal ↗"  `path: [2,2,1,0,0]`

```
  ·    ·    ·    ■    ■    ← row 0
  ·    ·    ■    ·    ·    ← row 1
  ■    ■    ·    ·    ·    ← row 2
```

---

### Linha 8 — "Z cima→baixo"  `path: [0,0,1,2,2]`

> ⚠️ Caminho idêntico à Linha 6. Intencionalmente duplicado no backend para peso extra de RTP.

```
  ■    ■    ·    ·    ·    ← row 0
  ·    ·    ■    ·    ·    ← row 1
  ·    ·    ·    ■    ■    ← row 2
```

---

### Linha 9 — "Z invertido"  `path: [2,2,1,0,0]`

> ⚠️ Caminho idêntico à Linha 7. Intencionalmente duplicado no backend para peso extra de RTP.

```
  ·    ·    ·    ■    ■    ← row 0
  ·    ·    ■    ·    ·    ← row 1
  ■    ■    ·    ·    ·    ← row 2
```

---

### Linha 10 — "Escada ↘"  `path: [0,1,1,2,2]`

```
  ■    ·    ·    ·    ·    ← row 0
  ·    ■    ■    ·    ·    ← row 1
  ·    ·    ·    ■    ■    ← row 2
```

---

### Linha 11 — "Escada ↗"  `path: [2,1,1,0,0]`

```
  ·    ·    ·    ■    ■    ← row 0
  ·    ■    ■    ·    ·    ← row 1
  ■    ·    ·    ·    ·    ← row 2
```

---

### Linha 14 — "Onda suave ↘"  `path: [0,1,1,1,2]`

```
  ■    ·    ·    ·    ·    ← row 0
  ·    ■    ■    ■    ·    ← row 1
  ·    ·    ·    ·    ■    ← row 2
```

---

### Linha 15 — "Onda suave ↗"  `path: [2,1,1,1,0]`

```
  ·    ·    ·    ·    ■    ← row 0
  ·    ■    ■    ■    ·    ← row 1
  ■    ·    ·    ·    ·    ← row 2
```

---

### Linha 16 — "Topo-meio-baixo"  `path: [0,1,2,1,0]`

> ⚠️ Caminho idêntico à Linha 4. Intencionalmente duplicado no backend para peso extra de RTP.

```
  ■    ·    ·    ·    ■    ← row 0
  ·    ■    ·    ■    ·    ← row 1
  ·    ·    ■    ·    ·    ← row 2
```

---

### Linha 20 — "Cruzada central"  `path: [1,0,1,2,1]`

```
  ·    ■    ·    ·    ·    ← row 0
  ■    ·    ■    ·    ■    ← row 1
  ·    ·    ·    ■    ·    ← row 2
```

---

## Níveis de Vitória

| Nível   | Condição               |
|---------|------------------------|
| none    | totalWin = 0           |
| small   | totalWin > 0           |
| big     | totalWin ≥ 5× totalBet |
| mega    | totalWin ≥ 20× totalBet|
| jackpot | totalWin ≥ 50× totalBet|
