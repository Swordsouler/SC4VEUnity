# SVENMultimodality

Système de contrôle vocal multimodal pour Unity VR, basé sur le framework SVEN (Semantized Virtual ENvironment). Permet de manipuler des objets 3D par commandes vocales en français et en anglais via reconnaissance vocale locale (Whisper ou Vosk) et synthèse vocale locale (Piper).

---

## Table des matières

1. [Architecture](#architecture)
2. [Speech-To-Text (STT)](#speech-to-text-stt)
   - [Whisper (recommandé)](#whisper-recommandé)
   - [Vosk (alternatif)](#vosk-alternatif)
3. [Text-To-Speech (TTS)](#text-to-speech-tts)
   - [Piper](#piper)
4. [Reconnaissance d'intentions](#reconnaissance-dintentions)
   - [Mode RuleBased](#mode-rulebased)
   - [Mode LLM — OpenAI](#mode-llm--openai)
   - [Mode LLM — Serveur local](#mode-llm--serveur-local)
5. [Commandes disponibles](#commandes-disponibles)
6. [Configuration Inspector](#configuration-inspector)
7. [Structure StreamingAssets](#structure-streamingassets)
8. [Dépendances](#dépendances)

---

## Architecture

```
Microphone
    │
    ▼
VoiceProcessor  (VAD / capture de trames)
    │  OnFrameCaptured / OnRecordingStop
    ▼
BaseSpeechToText
 ├── WhisperSpeechToText   ← offline, très bon FR/EN
 └── VoskSpeechToText      ← offline, streaming temps réel
    │  OnTranscriptionResult (JSON Vosk-compatible)
    ▼
MultimodalityController
 ├── [Mode LLM]        → OpenAI gpt-4o-mini / gpt-4o ou serveur local
 └── [Mode RuleBased]  → FrenchStemmer + regex + ActionMappings
    │
    ▼
CommandExecutor  (CreateCommand, ColorizeCommand, MoveCommand…)
    │
    ▼
Scene Unity (objets SVEN ontologie RDF/OWL)
    │
    ▼
PiperTextToSpeech  (retour vocal TTS optionnel)
```

---

## Speech-To-Text (STT)

### Whisper (recommandé)

Whisper offre une excellente précision en français et en anglais, y compris sur du vocabulaire spécialisé. Il fonctionne entièrement hors-ligne.

#### Téléchargement des modèles GGML

Les modèles sont hébergés sur HuggingFace :  
**https://huggingface.co/ggerganov/whisper.cpp/tree/main**

| Modèle | Taille | Qualité | Recommandation |
|--------|--------|---------|----------------|
| `ggml-tiny.bin` | ~75 Mo | Basique | Tests rapides |
| `ggml-base.bin` | ~142 Mo | Correcte | — |
| `ggml-small.bin` | ~466 Mo | Bonne | **Usage quotidien** |
| `ggml-medium.bin` | ~1,5 Go | Très bonne | Meilleure précision |

Télécharger le fichier `.bin` choisi et le placer dans :
```
Assets/StreamingAssets/Whisper/
```

> **Note :** Ce dossier est exclu du dépôt Git (`.gitignore`). Le modèle doit être téléchargé manuellement sur chaque poste de travail.

#### Installation du package Unity

Le package `com.whisper.unity` est déjà référencé dans `Packages/manifest.json`. Unity le télécharge automatiquement.

Si ce n'est pas le cas, l'ajouter via **Window → Package Manager → Add package from git URL** :
```
https://github.com/Macoron/whisper.unity.git
```

#### Configuration dans l'Inspector

Ajouter le composant `WhisperSpeechToText` sur un GameObject :

| Champ | Valeur |
|-------|--------|
| **Whisper Manager** | Référence vers le composant `WhisperManager` de la scène |
| **Voice Processor** | Référence vers le composant `VoiceProcessor` de la scène |
| **Auto Start** | ✅ (démarre l'écoute automatiquement) |
| **Push To Talk** | ☐ (ou ✅ pour mode bouton, touche `T`) |

Sur le composant `WhisperManager` :

| Champ | Valeur |
|-------|--------|
| **Model Path** | `StreamingAssets/Whisper/ggml-small.bin` |
| **Language** | `fr` (français) ou laisser vide pour détection automatique |
| **Enable Tokens** | ✅ (activé automatiquement au démarrage) |
| **Tokens Timestamps** | ✅ (activé automatiquement au démarrage) |

Sur le composant `VoiceProcessor` :

| Champ | Valeur |
|-------|--------|
| **Auto Detect** | ✅ **OBLIGATOIRE** pour le mode VAD (sans PTT) |
| **Sample Rate** | `16000` |

---

### Vosk (alternatif)

Vosk est plus adapté au streaming temps réel mais moins précis en français (confusion fréquente de mots similaires).

#### Téléchargement des modèles

**https://alphacephei.com/vosk/models**

| Modèle | Langue | Taille |
|--------|--------|--------|
| `vosk-model-small-fr-0.22` | Français (léger) | ~41 Mo |
| `vosk-model-fr-0.22` | Français (complet) | ~1,4 Go |
| `vosk-model-small-en-us-0.15` | Anglais (léger) | ~40 Mo |
| `vosk-model-en-us-0.22` | Anglais (complet) | ~1,8 Go |

Placer le fichier `.zip` dans :
```
Assets/StreamingAssets/
```

#### Configuration dans l'Inspector

| Champ | Valeur |
|-------|--------|
| **Model Path** | Nom du fichier zip (ex: `vosk-model-small-fr-0.22.zip`) |
| **Voice Processor** | Référence vers `VoiceProcessor` |
| **Auto Start** | ✅ |
| **Push To Talk** | ☐ |
| **Max Alternatives** | `1` |

---

## Text-To-Speech (TTS)

### Piper

Piper est un moteur TTS neuronal offline rapide, avec une bonne qualité en français et en anglais. Aucun entraînement requis, des modèles pré-entraînés sont disponibles.

#### Téléchargement de l'exécutable Piper

**https://github.com/rhasspy/piper/releases/latest**

Télécharger l'archive correspondant à la plateforme (ex: `piper_windows_amd64.zip`), extraire et placer le contenu dans :
```
Assets/StreamingAssets/Piper/
```

Le dossier doit contenir `piper.exe` (Windows) ou `piper` (Linux/macOS).

#### Téléchargement des modèles vocaux

**https://huggingface.co/rhasspy/piper-voices/tree/main**

Pour chaque modèle, deux fichiers sont nécessaires : le `.onnx` et le `.onnx.json`.

| Langue | Modèle | Qualité | Lien |
|--------|--------|---------|------|
| Français | `fr/fr_FR/mls_1840/medium/` | Moyenne | [Télécharger](https://huggingface.co/rhasspy/piper-voices/tree/main/fr/fr_FR/mls_1840/medium) |
| Anglais | `en/en_US/lessac/medium/` | Moyenne | [Télécharger](https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/lessac/medium) |

Placer les fichiers dans :
```
Assets/StreamingAssets/Piper/models/
    fr-fr-mls_1840-medium.onnx
    fr-fr-mls_1840-medium.onnx.json
    en-us-lessac-medium.onnx
    en-us-lessac-medium.onnx.json
```

> **Note :** Le dossier `Piper/` est exclu du dépôt Git (`.gitignore`). Les fichiers doivent être téléchargés manuellement.

#### Configuration dans l'Inspector

Ajouter le composant `PiperTextToSpeech` sur un GameObject (un `AudioSource` sera ajouté automatiquement) :

| Champ | Valeur par défaut | Description |
|-------|-------------------|-------------|
| **Piper Exe Path** | `Piper/piper.exe` | Chemin vers piper.exe, relatif à StreamingAssets |
| **French Model Path** | `Piper/models/fr-fr-mls_1840-medium.onnx` | Modèle français |
| **English Model Path** | `Piper/models/en-us-lessac-medium.onnx` | Modèle anglais |
| **Language** | `French` | Langue active (`French` ou `English`) |

#### API C#

```csharp
PiperTextToSpeech tts = GetComponent<PiperTextToSpeech>();

tts.Speak("Bonjour, que souhaitez-vous créer ?");
tts.SetLanguage(Language.English);
tts.Speak("Hello, what would you like to create?");
tts.StopSpeaking(); // vide la file et arrête la lecture

tts.OnSpeechStart += () => Debug.Log("Début de la parole");
tts.OnSpeechEnd   += () => Debug.Log("Fin de la parole");
```

---

## Reconnaissance d'intentions

Le `MultimodalityController` supporte deux modes configurables dans l'Inspector (`Recognizer Mode`).

---

### Mode RuleBased

Entièrement offline, sans réseau ni modèle externe :
- Racinisation française (`FrenchStemmer`) + expressions régulières
- Table `ActionMappings` associant mots-clés → types de commandes
- Vocabulaire Vosk injecté dynamiquement depuis l'ontologie pour réduire les confusions phonétiques

Recommandé pour un usage hors-ligne garanti et une latence minimale (< 50 ms).  
→ Voir `COMMANDS.md` pour ajouter de nouvelles commandes.

---

### Mode LLM — OpenAI

Utilise l'API OpenAI pour interpréter la commande vocale en JSON :

- **Fast path** : `gpt-4o-mini` (rapide, peu coûteux)
- **Fallback automatique** : `gpt-4o` si la validation échoue (mauvais JSON, filtre manquant, etc.)
- Le prompt système est compilé **une seule fois** au démarrage et réutilisé à chaque appel → OpenAI le met en cache côté serveur (~50 % de coût prompt économisé)

**Configuration Inspector :**

| Champ | Valeur |
|-------|--------|
| **Llm Service** | `OpenAI` |
| **Open Ai Api Key** | Clé API OpenAI (`sk-…`) |
| **Fast Model** | `gpt-4o-mini` (défaut) |
| **Precise Model** | `gpt-4o` (défaut) |

---

### Mode LLM — Serveur local

Utilise un LLM tournant en local via une API compatible OpenAI (LM Studio, Ollama, llama.cpp).  
Aucune clé API requise, tout reste hors-ligne.

#### Choix du modèle

| Modèle | VRAM (Q4_K_M) | Vitesse (RTX 4090) | Fiabilité JSON | Recommandation |
|--------|--------------|-------------------|----------------|----------------|
| **Qwen3-4B-Instruct** | ~3 Go | ~120 tok/s | ⭐⭐⭐⭐⭐ | **Premier choix** |
| **Phi-4-mini-instruct** | ~3 Go | ~110 tok/s | ⭐⭐⭐⭐⭐ | Très bon alternatif |
| **Llama-3.2-3B-Instruct** | ~2,5 Go | ~150 tok/s | ⭐⭐⭐⭐ | Si VRAM limitée |
| Mistral-7B-Instruct-v0.3 | ~5 Go | ~70 tok/s | ⭐⭐⭐⭐ | Plus lent |
| Mistral-Nemo-12B | ~8 Go | ~35 tok/s | ⭐⭐⭐⭐⭐ | Si VRAM suffisante |

Tous les modèles ci-dessus sont à télécharger au format **GGUF Q4_K_M**.

> **Note latence :** En mode local, les exemples sont automatiquement retirés du prompt (~3 000 tokens au lieu de ~6 500). Le premier appel inclut le prefill (~1-2 s). Les appels suivants réutilisent le KV cache → latence effective ~0,5-1 s avec Qwen3-4B sur RTX 4090.

#### Configuration LM Studio (recommandé)

1. Télécharger [LM Studio](https://lmstudio.ai/)
2. Rechercher et charger `Qwen3-4B-Instruct` (format GGUF Q4_K_M)
3. Réglages du modèle :

   | Paramètre | Valeur |
   |-----------|--------|
   | **Context length** | `8192` minimum (le prompt système seul fait ~6 500 tokens avec les exemples) |
   | **GPU layers** | `MAX` (tout sur GPU, pas de CPU offload) |
   | **Flash Attention** | `ON` |
   | **Keep model loaded** | `ON` (évite le rechargement entre les phrases) |

4. Démarrer le serveur local dans LM Studio (onglet **Local Server**) → URL par défaut : `http://localhost:1234/v1`

#### Configuration Inspector Unity

| Champ | Valeur |
|-------|--------|
| **Llm Service** | `Local` |
| **Local Llm Url** | `http://localhost:1234/v1` (LM Studio) ou `http://localhost:11434/v1` (Ollama) |

#### Configuration Ollama (alternative)

```bash
# Installer le modèle
ollama pull qwen3:4b

# Le serveur démarre automatiquement, API disponible sur http://localhost:11434
```

Ollama expose une API compatible OpenAI sur `/v1` — aucune autre modification n'est nécessaire.

---

## Commandes disponibles

| Commande | Description | Exemple |
|----------|-------------|---------|
| `CreateCommand` | Créer un objet | « Crée une pomme rouge » |
| `DestroyCommand` | Supprimer un objet | « Détruis la citrouille » |
| `ColorizeCommand` | Changer la couleur | « Colorie les pommes en bleu » |
| `MoveCommand` | Déplacer un objet | « Déplace la banane ici » |
| `ScaleUpCommand` | Agrandir | « Agrandis la pomme » |
| `ScaleDownCommand` | Rétrécir | « Rétrécis la carotte » |
| `HideCommand` | Masquer | « Cache la banane » |
| `ShowCommand` | Afficher | « Montre la citrouille » |
| `SelectCommand` | Sélectionner | « Sélectionne les pommes rouges » |
| `UnselectCommand` | Désélectionner | « Désélectionne tout » |
| `EventCommand` | Événement système | « Sauvegarde la scène » |

---

## Configuration Inspector

### MultimodalityController

| Champ | Type | Description |
|-------|------|-------------|
| **Speech To Text** | `BaseSpeechToText` | Assigner `WhisperSpeechToText` ou `VoskSpeechToText` |
| **Piper TTS** | `PiperTextToSpeech` | Optionnel, pour les retours vocaux |
| **Mode** | `LLM` / `RuleBased` | Mode de reconnaissance d'intentions |
| **API Key** | `string` | Clé OpenAI (mode LLM uniquement) |

### SSTResultTMP

Affiche la transcription en temps réel dans un `TextMeshProUGUI` :

| Champ | Type | Description |
|-------|------|-------------|
| **Speech To Text** | `BaseSpeechToText` | Même référence que MultimodalityController |
| **Result Text** | `TextMeshProUGUI` | Composant UI à mettre à jour |

> `VoskResultTMP` est conservé comme alias obsolète de `SSTResultTMP` pour la compatibilité des scènes existantes.

---

## Structure StreamingAssets

```
Assets/StreamingAssets/
├── Whisper/                    ← ignoré par Git
│   └── ggml-small.bin          ← modèle Whisper (télécharger manuellement)
├── Piper/                      ← ignoré par Git
│   ├── piper.exe               ← exécutable Piper (télécharger manuellement)
│   └── models/
│       ├── fr-fr-mls_1840-medium.onnx
│       ├── fr-fr-mls_1840-medium.onnx.json
│       ├── en-us-lessac-medium.onnx
│       └── en-us-lessac-medium.onnx.json
└── vosk-model-small-fr-0.22.zip  ← modèle Vosk (si utilisé, ignoré par Git)
```

---

## Dépendances

| Package | Source | Usage |
|---------|--------|-------|
| `com.whisper.unity` | [GitHub](https://github.com/Macoron/whisper.unity) | STT Whisper |
| `com.utilities.encoder.pcm` | Package Manager | Encodage audio |
| Vosk Unity | [Package](https://github.com/alphacep/vosk-unity-asset) | STT Vosk |
| NaughtyAttributes | Package Manager | Inspector UI |
| TextMeshPro | Package Manager | Affichage UI |
| XR Interaction Toolkit | Package Manager | Interactions VR |
| OpenXR | Package Manager | Backend XR |
| dotNetRDF | Assets | Ontologie SVEN RDF/OWL |
