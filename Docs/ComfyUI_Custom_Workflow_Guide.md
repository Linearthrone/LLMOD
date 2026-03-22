# Custom ComfyUI Workflow for Image Generation

House Victoria can use a custom ComfyUI workflow for image generation instead of the built-in SD/Flux workflows. This lets you use your own conditioning, models, and pipeline (e.g. an "AIGEN" workflow with positive and negative prompts).

## Setup

1. In **Settings → Image Generation (ComfyUI)**, set **Custom workflow** to the path of your workflow JSON file.
2. The file must be in ComfyUI API format (use **Save (API format)** in ComfyUI).

## Placeholders

Use these placeholders in your workflow JSON. They are replaced at runtime:

| Placeholder | Replaced with | Example |
|-------------|---------------|---------|
| `{{positive}}` | User's image prompt | `"text": "{{positive}}"` |
| `{{negative}}` | Default negative prompt | `"text": "{{negative}}"` |
| `{{width}}` | Image width (number) | `"width": {{width}}` |
| `{{height}}` | Image height (number) | `"height": {{height}}` |
| `{{seed}}` | Random seed (number) | `"seed": {{seed}}` |
| `{{filename_prefix}}` | Output filename prefix | `"filename_prefix": "{{filename_prefix}}"` |

### Example: CLIPTextEncode nodes

For positive prompt (your custom conditioning):
```json
"text": "{{positive}}"
```

For negative prompt:
```json
"text": "{{negative}}"
```

### Example: EmptyLatentImage / EmptySD3LatentImage

```json
"width": {{width}},
"height": {{height}}
```

### Example: KSampler

```json
"seed": {{seed}}
```

### Example: SaveImage

```json
"filename_prefix": "{{filename_prefix}}"
```

## Creating Your Workflow

1. Build your workflow in ComfyUI (e.g. with your AIGEN conditioning constraints).
2. In each node that should use runtime values, replace the static values with the placeholders above.
3. Save via **ComfyUI → Save (API format)** or **Save** and ensure the JSON has the API structure (node IDs as keys, `class_type`, `inputs`).
4. Point House Victoria to the saved `.json` file in Settings.

## File Format

The workflow can be:
- **Raw workflow**: `{ "4": {...}, "5": {...}, ... }`
- **Wrapped**: `{ "prompt": { "4": {...}, ... } }`

Both are supported.
