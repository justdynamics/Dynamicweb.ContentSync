# Dynamicweb.ContentSync

A DynamicWeb AppStore app that serializes and deserializes content trees to/from YAML files on disk, enabling content to be version-controlled and deployed alongside code.

The DynamicWeb equivalent of **Sitecore Unicorn** — replacing manual content sync with automated, developer-friendly content synchronization.

## How It Works

ContentSync uses **predicates** to define which content trees to synchronize. Each predicate targets a root page in a specific area. Pages under that root (including grid rows and paragraphs) are serialized to YAML files in a mirror-tree folder layout.

### Default Flow: Folder-Based Sync

This is the primary workflow for teams using source control (Git) to move content between environments.

```
Source Environment                          Target Environment
(e.g. Development)                          (e.g. Staging)

  DynamicWeb DB                               DynamicWeb DB
       |                                           ^
       | Serialize (scheduled task)                | Deserialize (scheduled task)
       v                                           |
  Files/System/ContentSync/                   Files/System/ContentSync/
       SerializeRoot/                              SerializeRoot/
         Swift 2/                                    Swift 2/
           area.yml                                    area.yml
           Customer Center/                            Customer Center/
             page.yml                                    page.yml
             grid-row-1/                                 grid-row-1/
               grid-row.yml                                grid-row.yml
               paragraph-c1-1.yml                          paragraph-c1-1.yml
               ...                                         ...
```

**Steps:**

1. **Configure predicates** in the admin UI (Settings > Content > Sync > Predicates) pointing to the content trees you want to sync
2. **Run the Serialize scheduled task** — content matching predicates is written as YAML to `SerializeRoot/`
3. **Commit the YAML files** to your Git repository
4. **Deploy to target environment** — the YAML files arrive via Git pull/deploy
5. **Run the Deserialize scheduled task** — YAML is read and applied to the target database
6. Content is matched by **PageUniqueId (GUID)** — existing pages are updated, new pages are created

### Ad-Hoc Flow: Single Page Export/Import

For moving individual pages between environments without a full sync cycle (e.g. moving a landing page from staging to production).

**Export (Serialize):**

1. Navigate to any page in the DynamicWeb admin content tree
2. Open the page edit screen
3. Click **"Serialize subtree"** in the Actions menu
4. A zip file downloads containing the page and all its children as YAML
5. The zip is also saved to `Files/System/ContentSync/Download/`

**Import (Deserialize):**

1. Place the zip file in `Files/System/ContentSync/Upload/` on the target environment
2. Run the **Deserialize scheduled task** with the **Zip File** parameter set to the filename (e.g. `ContentSync_CustomerCenter_2026-03-22.zip`)
3. The zip is extracted, validated, and applied to the content tree

## Content Model

ContentSync serializes the full DynamicWeb content hierarchy:

| Level | What's serialized |
|-------|-------------------|
| **Area** | Area metadata |
| **Page** | Name, sort order, item type, item fields, property fields (Icon, SubmenuType) |
| **Grid Row** | Layout settings (spacing, container width, visual properties) |
| **Paragraph** | Content, item type fields, column attribution |

Identity is based on **PageUniqueId (GUID)**, not numeric IDs. This means content can move between environments where numeric IDs differ.

## Configuration

All settings are managed from the DynamicWeb admin UI at **Settings > Content > Sync**, or by editing the config file directly at `Files/ContentSync.config.json`.

### Settings Screen

<!-- TODO: Add screenshot of the settings screen -->

| Setting | Description |
|---------|-------------|
| **Output Directory** | Top-level folder relative to `Files/System`. Subfolders are created automatically: `SerializeRoot`, `Upload`, `Download` |
| **Log Level** | Logging verbosity: Info, Debug, Warn, Error |
| **Dry Run** | When enabled, sync operations log what would happen without making changes |
| **Conflict Strategy** | How to handle conflicts. Currently: Source Wins (serialized files overwrite target) |

### Predicate Management

<!-- TODO: Add screenshot of the predicates list screen -->

Predicates define which content trees to synchronize. Manage them at **Settings > Content > Sync > Predicates**.

<!-- TODO: Add screenshot of the predicate edit screen with area selector and page picker -->

| Field | Description |
|-------|-------------|
| **Name** | Unique name for this predicate |
| **Area** | DynamicWeb area containing the content tree (dropdown) |
| **Page** | Root page for the predicate (content tree picker) |
| **Excludes** | Paths to exclude from sync (one per line, optional) |

### Config File

The config file at `Files/ContentSync.config.json` is the source of truth. The admin UI reads and writes this file. Manual edits are reflected on the next screen load.

```json
{
  "outputDirectory": "ContentSync",
  "logLevel": "info",
  "dryRun": false,
  "conflictStrategy": "source-wins",
  "predicates": [
    {
      "name": "Customer Center",
      "path": "/Customer Center",
      "areaId": 3,
      "pageId": 8385,
      "excludes": []
    }
  ]
}
```

## Folder Structure

```
Files/System/{OutputDirectory}/
  SerializeRoot/     YAML files — scheduled tasks read/write here
  Upload/            Drop .zip files here for zip-based import
  Download/          Ad-hoc serialize saves zip copies here
```

## Scheduled Tasks

Two scheduled tasks are available in the DynamicWeb admin at **Settings > Integration > Scheduled Tasks**:

| Task | Description |
|------|-------------|
| **ContentSync - Serialize** | Serializes content matching all predicates to YAML in `SerializeRoot/` |
| **ContentSync - Deserialize** | Deserializes YAML from `SerializeRoot/` (folder mode) or from a zip in `Upload/` (zip mode) |

The Deserialize task has an optional **Zip File** parameter. Leave empty for folder mode, or set to a filename (e.g. `import.zip`) to import from the Upload folder.

## Admin UI

### Sync Node in Navigation Tree

<!-- TODO: Add screenshot of the Settings > Content > Sync tree node -->

The Sync node appears under **Settings > Content** with a Predicates sub-node.

### Serialize Action on Pages

<!-- TODO: Add screenshot of the page edit screen Actions menu showing "Serialize subtree" -->

The **"Serialize subtree"** action appears in the Actions menu on every page edit screen, alongside Preview and Paragraphs.

## API Commands & CI/CD Integration

ContentSync exposes two Management API commands for immediate serialization and deserialization. These enable automated workflows triggered by CI/CD pipelines, Git hooks, or the [DynamicWeb CLI](https://github.com/dynamicweb/CLI).

### Available Commands

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/Admin/Api/ContentSyncSerialize` | POST | Serialize all predicate-matched content to `SerializeRoot/` |
| `/Admin/Api/ContentSyncDeserialize` | POST | Deserialize YAML from `SerializeRoot/` into the database |

Authentication: `Authorization: Bearer {API-Key}` (Management API key)

### Example: curl

```bash
# Serialize content on source environment
curl -X POST https://source.example.com/Admin/Api/ContentSyncSerialize \
  -H "Authorization: Bearer CLD.your-api-key-here"

# Deserialize content on target environment (after YAML files are deployed)
curl -X POST https://target.example.com/Admin/Api/ContentSyncDeserialize \
  -H "Authorization: Bearer CLD.your-api-key-here"
```

### Example: DynamicWeb CLI

```bash
# Using the DW CLI (https://github.com/dynamicweb/CLI)
dw env production
dw command ContentSyncDeserialize
```

### Example: GitHub Actions

```yaml
- name: Deploy content
  run: |
    # After deploying code + YAML files to the target environment
    curl -X POST ${{ secrets.DW_HOST }}/Admin/Api/ContentSyncDeserialize \
      -H "Authorization: Bearer ${{ secrets.DW_API_KEY }}"
```

### Git-Based Workflow

The typical CI/CD flow for content synchronization:

```
Developer commits YAML files
        ↓
Git push → CI/CD pipeline
        ↓
Deploy to target (code + YAML files land in SerializeRoot/)
        ↓
POST /Admin/Api/ContentSyncDeserialize
        ↓
Content is immediately applied — no scheduled task delay
```

## Installation

1. Build the project:
   ```
   dotnet build src/Dynamicweb.ContentSync/ -c Release
   ```

2. Copy `Dynamicweb.ContentSync.dll` to your DynamicWeb instance's `bin/` directory

3. Restart the DynamicWeb application

4. Navigate to **Settings > Content > Sync** to configure

## Tech Stack

- .NET 8.0
- DynamicWeb 10.23.9+
- YamlDotNet 13.7.1
- System.IO.Compression (built-in)

## License

Open source — no licensing required.
