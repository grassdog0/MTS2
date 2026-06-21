#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif
#include <windows.h>
#include <commctrl.h>
#include <shellapi.h>
#include <shlobj.h>
#include <uxtheme.h>
#include <wchar.h>
#include <stdio.h>
#include <stdarg.h>

#define MAX_MODS 1024
#define MAX_TEXT 512
#define MAX_BUF 1048576
#define MAX_DEPS 16
#define MAX_PROFILE 128

typedef struct ModInfo {
    WCHAR id[MAX_TEXT];
    WCHAR name[MAX_TEXT];
    WCHAR source[32];
    WCHAR manifest[MAX_PATH * 2];
    WCHAR deps[MAX_DEPS][MAX_TEXT];
    int depCount;
    int depth;
    BOOL affectsGameplay;
} ModInfo;

static HINSTANCE g_instance;
static HWND g_list, g_status, g_gamePath, g_settingsPath, g_title, g_subtitle;
static HWND g_btnRefresh, g_btnVanilla, g_btnLaunch, g_btnUp, g_btnDown, g_btnSaveOrder, g_btnResetOrder;
static HWND g_orderEdit, g_btnApplyOrder, g_profileCombo, g_btnSaveProfile, g_btnLoadProfile;
static HFONT g_font, g_titleFont;
static HBRUSH g_bgBrush;
static COLORREF g_bgColor = RGB(246, 243, 235);
static COLORREF g_textColor = RGB(45, 38, 32);
static WCHAR g_appDir[MAX_PATH * 2], g_gameDir[MAX_PATH * 2], g_settingsFile[MAX_PATH * 2];
static WCHAR g_steamExe[MAX_PATH * 2], g_steamArgs[8192];
static DWORD g_waitForPid;
static BOOL g_chinese, g_diagnose, g_selfTestOrder;
static ModInfo g_mods[MAX_MODS];
static int g_modCount;
static BOOL g_enforcingChecks;
static WCHAR g_savedOrder[MAX_MODS][MAX_TEXT];
static int g_savedOrderCount;
static WCHAR g_savedEnabled[MAX_MODS][MAX_TEXT];
static int g_savedEnabledCount;

static const WCHAR* T(const WCHAR* zh, const WCHAR* en) { (void)zh; return en; }
static char* ReplaceSpan(char* src, DWORD* size, DWORD start, DWORD oldLen, const char* newText);
static int GetSelectedListIndex(void);
static void RebuildListPreservingChecks(int selectedIndex);
static void RepairDependencyOrderInPlace(void);

static void DirName(WCHAR* path) {
    WCHAR* slash = wcsrchr(path, L'\\');
    if (slash) *slash = 0;
}

static BOOL FileExistsW2(const WCHAR* path) {
    DWORD a = GetFileAttributesW(path);
    return a != INVALID_FILE_ATTRIBUTES && !(a & FILE_ATTRIBUTE_DIRECTORY);
}

static BOOL DirExistsW2(const WCHAR* path) {
    DWORD a = GetFileAttributesW(path);
    return a != INVALID_FILE_ATTRIBUTES && (a & FILE_ATTRIBUTE_DIRECTORY);
}

static void JoinPath(WCHAR* out, size_t cap, const WCHAR* a, const WCHAR* b) {
    if (cap == 0) return;
    wcsncpy(out, a, cap - 1);
    out[cap - 1] = 0;
    size_t len = wcslen(out);
    if (len + 1 < cap && len > 0 && out[len - 1] != L'\\') {
        out[len++] = L'\\';
        out[len] = 0;
    }
    if (len < cap - 1) {
        wcsncat(out, b, cap - len - 1);
    }
}

static const char* SkipWs(const char* p) {
    while (*p == ' ' || *p == '\t' || *p == '\r' || *p == '\n') ++p;
    return p;
}

static char* ReadFileBytes(const WCHAR* path, DWORD* outSize) {
    HANDLE h = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0, NULL);
    if (h == INVALID_HANDLE_VALUE) return NULL;
    DWORD size = GetFileSize(h, NULL);
    if (size == INVALID_FILE_SIZE || size > MAX_BUF * 8) { CloseHandle(h); return NULL; }
    char* data = (char*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, size + 1);
    if (!data) { CloseHandle(h); return NULL; }
    DWORD read = 0;
    ReadFile(h, data, size, &read, NULL);
    CloseHandle(h);
    data[read] = 0;
    if (outSize) *outSize = read;
    return data;
}

static const char* JsonStart(const char* json) {
    const unsigned char* p = (const unsigned char*)json;
    if (p[0] == 0xEF && p[1] == 0xBB && p[2] == 0xBF) p += 3;
    return SkipWs((const char*)p);
}

static BOOL WriteFileBytes(const WCHAR* path, const char* data, DWORD size) {
    HANDLE h = CreateFileW(path, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, 0, NULL);
    if (h == INVALID_HANDLE_VALUE) return FALSE;
    DWORD written = 0;
    BOOL ok = WriteFile(h, data, size, &written, NULL);
    CloseHandle(h);
    return ok && written == size;
}

static void Utf8ToWide2(const char* s, int len, WCHAR* out, int cap) {
    if (cap <= 0) return;
    int n = MultiByteToWideChar(CP_UTF8, 0, s, len, out, cap - 1);
    out[n < 0 ? 0 : n] = 0;
}

static int WideToUtf82(const WCHAR* s, char* out, int cap) {
    return WideCharToMultiByte(CP_UTF8, 0, s, -1, out, cap, NULL, NULL);
}

static BOOL JsonStringValue(const char* json, const char* key, WCHAR* out, int cap) {
    json = JsonStart(json);
    char pattern[128];
    snprintf(pattern, sizeof(pattern), "\"%s\"", key);
    const char* p = strstr(json, pattern);
    if (!p) return FALSE;
    p = strchr(p + strlen(pattern), ':');
    if (!p) return FALSE;
    p = SkipWs(p + 1);
    if (*p != '"') return FALSE;
    ++p;
    const char* start = p;
    while (*p && *p != '"') {
        if (*p == '\\' && p[1]) p += 2;
        else ++p;
    }
    Utf8ToWide2(start, (int)(p - start), out, cap);
    return TRUE;
}

static void JsonDependencyIds(const char* json, WCHAR deps[MAX_DEPS][MAX_TEXT], int* depCount) {
    *depCount = 0;
    const char* p = strstr(json, "\"dependencies\"");
    if (!p) return;
    p = strchr(p, '[');
    if (!p) return;
    const char* end = strchr(p, ']');
    if (!end) return;

    while (p && p < end && *depCount < MAX_DEPS) {
        p = SkipWs(p + 1);
        if (p >= end) break;
        if (*p == ',') {
            continue;
        }
        if (*p == '{') {
            const char* objectEnd = strchr(p, '}');
            if (!objectEnd || objectEnd > end) objectEnd = end;
            const char* idKey = strstr(p, "\"id\"");
            if (idKey && idKey < objectEnd) {
                const char* colon = strchr(idKey, ':');
                if (colon && colon < objectEnd) {
                    const char* value = SkipWs(colon + 1);
                    if (*value == '"') {
                        ++value;
                        const char* start = value;
                        while (*value && value < objectEnd && *value != '"') {
                            if (*value == '\\' && value[1]) value += 2;
                            else ++value;
                        }
                        if (value > start) {
                            Utf8ToWide2(start, (int)(value - start), deps[*depCount], MAX_TEXT);
                            if (deps[*depCount][0]) ++(*depCount);
                        }
                    }
                }
            }
            p = objectEnd + 1;
            continue;
        }
        if (*p == '"') {
            const char* value = p + 1;
            const char* start = value;
            while (*value && value < end && *value != '"') {
                if (*value == '\\' && value[1]) value += 2;
                else ++value;
            }
            if (value > start) {
                Utf8ToWide2(start, (int)(value - start), deps[*depCount], MAX_TEXT);
                if (deps[*depCount][0]) ++(*depCount);
            }
            p = value + 1;
            continue;
        }
        ++p;
#if 0
        const char* idKey = strstr(p, "\"id\"");
        const char* nextString = strchr(p + 1, '"');
        if ((!idKey || idKey > end) && nextString && nextString < end) {
            const char* value = nextString + 1;
            const char* start = value;
            while (*value && value < end && *value != '"') {
                if (*value == '\\' && value[1]) value += 2;
                else ++value;
            }
            if (value > start) {
                Utf8ToWide2(start, (int)(value - start), deps[*depCount], MAX_TEXT);
                if (deps[*depCount][0]) ++(*depCount);
            }
            p = value + 1;
            continue;
        }
        if (!idKey || idKey > end) break;
        const char* colon = strchr(idKey, ':');
        if (!colon || colon > end) break;
        const char* value = SkipWs(colon + 1);
        if (*value != '"') {
            p = idKey + 4;
            continue;
        }
        ++value;
        const char* start = value;
        while (*value && value < end && *value != '"') {
            if (*value == '\\' && value[1]) value += 2;
            else ++value;
        }
        if (value > start) {
            Utf8ToWide2(start, (int)(value - start), deps[*depCount], MAX_TEXT);
            if (deps[*depCount][0]) ++(*depCount);
        }
        p = value + 1;
#endif
    }
}

static BOOL JsonBoolAfterId(const char* json, const WCHAR* id) {
    char id8[MAX_TEXT * 4];
    WideToUtf82(id, id8, sizeof(id8));
    char pattern[MAX_TEXT * 4 + 16];
    snprintf(pattern, sizeof(pattern), "\"id\"%*s:%*s\"%s\"", 0, "", 0, "", id8);
    const char* p = strstr(json, id8);
    if (!p) return FALSE;
    const char* endObj = strchr(p, '}');
    if (!endObj) endObj = p + strlen(p);
    const char* e = strstr(p, "\"is_enabled\"");
    if (!e || e > endObj) return FALSE;
    e = strchr(e, ':');
    if (!e || e > endObj) return FALSE;
    e = SkipWs(e + 1);
    return strncmp(e, "true", 4) == 0;
}

static BOOL JsonBoolValue(const char* json, const char* key) {
    json = JsonStart(json);
    char pattern[128];
    snprintf(pattern, sizeof(pattern), "\"%s\"", key);
    const char* p = strstr(json, pattern);
    if (!p) return FALSE;
    p = strchr(p + strlen(pattern), ':');
    if (!p) return FALSE;
    p = SkipWs(p + 1);
    return strncmp(p, "true", 4) == 0;
}

static BOOL FindModListSpan(const char* json, DWORD size, DWORD* start, DWORD* len) {
    char* p = strstr((char*)json, "\"mod_list\"");
    if (!p) return FALSE;
    p = strchr(p, '[');
    if (!p) return FALSE;
    DWORD depth = 0;
    BOOL inString = FALSE;
    BOOL escaped = FALSE;
    DWORD begin = (DWORD)(p - json);
    for (DWORD i = begin; i < size; ++i) {
        char c = json[i];
        if (inString) {
            if (escaped) escaped = FALSE;
            else if (c == '\\') escaped = TRUE;
            else if (c == '"') inString = FALSE;
            continue;
        }
        if (c == '"') {
            inString = TRUE;
            continue;
        }
        if (c == '[') ++depth;
        else if (c == ']') {
            if (depth == 0) return FALSE;
            --depth;
            if (depth == 0) {
                *start = begin;
                *len = i - begin + 1;
                return TRUE;
            }
        }
    }
    return FALSE;
}

static BOOL JsonObjectIdInDiscoveredMods(const char* objectStart, const char* objectEnd) {
    const char* idKey = strstr(objectStart, "\"id\"");
    if (!idKey || idKey >= objectEnd) return FALSE;
    const char* colon = strchr(idKey, ':');
    if (!colon || colon >= objectEnd) return FALSE;
    const char* value = SkipWs(colon + 1);
    if (*value != '"') return FALSE;
    ++value;
    const char* start = value;
    while (value < objectEnd && *value && *value != '"') {
        if (*value == '\\' && value[1]) value += 2;
        else ++value;
    }
    if (value <= start) return FALSE;
    WCHAR id[MAX_TEXT];
    Utf8ToWide2(start, (int)(value - start), id, _countof(id));
    for (int i = 0; i < g_modCount; ++i) {
        if (_wcsicmp(g_mods[i].id, id) == 0) return TRUE;
    }
    return FALSE;
}

static BOOL IsChineseLanguage(const WCHAR* lang) {
    return _wcsnicmp(lang, L"zh", 2) == 0 || _wcsnicmp(lang, L"zhs", 3) == 0 || _wcsnicmp(lang, L"zht", 3) == 0 || _wcsicmp(lang, L"chi") == 0 || _wcsicmp(lang, L"chs") == 0;
}

static void AppendLog(const WCHAR* msg) {
    WCHAR dir[MAX_PATH * 2], path[MAX_PATH * 2], line[MAX_PATH * 4];
    JoinPath(dir, _countof(dir), g_appDir, L"ModTheSpire2Data");
    CreateDirectoryW(dir, NULL);
    JoinPath(path, _countof(path), dir, L"launcher.log");
    SYSTEMTIME st; GetLocalTime(&st);
    swprintf(line, _countof(line), L"%04d-%02d-%02d %02d:%02d:%02d %ls\r\n",
             st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond, msg);

    char utf8[MAX_PATH * 12];
    int len = WideCharToMultiByte(CP_UTF8, 0, line, -1, utf8, sizeof(utf8), NULL, NULL);
    if (len <= 1) return;

    HANDLE h = CreateFileW(path, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE) {
        WCHAR temp[MAX_PATH * 2];
        DWORD n = GetTempPathW(_countof(temp), temp);
        if (n == 0 || n >= _countof(temp)) return;
        JoinPath(path, _countof(path), temp, L"ModTheSpire2Launcher.log");
        h = CreateFileW(path, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
        if (h == INVALID_HANDLE_VALUE) return;
    }
    DWORD written = 0;
    WriteFile(h, utf8, (DWORD)(len - 1), &written, NULL);
    CloseHandle(h);
}

static void AppendLogf(const WCHAR* fmt, ...) {
    WCHAR msg[MAX_PATH * 4];
    va_list args;
    va_start(args, fmt);
    _vsnwprintf(msg, _countof(msg) - 1, fmt, args);
    msg[_countof(msg) - 1] = 0;
    va_end(args);
    AppendLog(msg);
}

static BOOL TrySetGameDir(const WCHAR* dir) {
    WCHAR exe[MAX_PATH * 2];
    JoinPath(exe, _countof(exe), dir, L"SlayTheSpire2.exe");
    if (!FileExistsW2(exe)) return FALSE;
    wcscpy(g_gameDir, dir);
    return TRUE;
}

static void FindGameDirFromAppDir(void) {
    WCHAR cur[MAX_PATH * 2];
    wcscpy(cur, g_appDir);
    for (int i = 0; i < 8 && cur[0]; ++i) {
        if (TrySetGameDir(cur)) return;
        WCHAR* name = wcsrchr(cur, L'\\');
        name = name ? name + 1 : cur;
        if (_wcsicmp(name, L"mods") == 0) {
            WCHAR parent[MAX_PATH * 2];
            wcscpy(parent, cur);
            DirName(parent);
            if (TrySetGameDir(parent)) return;
        }
        WCHAR parent[MAX_PATH * 2];
        wcscpy(parent, cur);
        DirName(parent);
        if (wcscmp(parent, cur) == 0) break;
        wcscpy(cur, parent);
    }
}

static void FindGameDirFromSteamCommand(void) {
    if (!g_steamExe[0]) return;
    WCHAR candidate[MAX_PATH * 2];
    wcsncpy(candidate, g_steamExe, _countof(candidate) - 1);
    candidate[_countof(candidate) - 1] = 0;
    DirName(candidate);
    TrySetGameDir(candidate);
}

static void FindGameDirFromWorkshopPath(void) {
    WCHAR cur[MAX_PATH * 2];
    wcsncpy(cur, g_appDir, _countof(cur) - 1);
    cur[_countof(cur) - 1] = 0;

    for (int i = 0; i < 12 && cur[0]; ++i) {
        WCHAR* name = wcsrchr(cur, L'\\');
        name = name ? name + 1 : cur;
        if (_wcsicmp(name, L"steamapps") == 0) {
            WCHAR candidate[MAX_PATH * 2];
            swprintf(candidate, _countof(candidate), L"%ls\\common\\Slay the Spire 2", cur);
            if (TrySetGameDir(candidate)) return;
        }

        WCHAR parent[MAX_PATH * 2];
        wcscpy(parent, cur);
        DirName(parent);
        if (wcscmp(parent, cur) == 0) break;
        wcscpy(cur, parent);
    }
}

static void FindGameDir(void) {
    FindGameDirFromAppDir();
    if (g_gameDir[0]) return;
    FindGameDirFromSteamCommand();
    if (g_gameDir[0]) return;
    FindGameDirFromWorkshopPath();
    if (g_gameDir[0]) return;
    const WCHAR* candidates[] = {
        L"E:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2",
        L"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Slay the Spire 2",
        L"C:\\Program Files\\Steam\\steamapps\\common\\Slay the Spire 2"
    };
    for (int i = 0; i < 3; ++i) {
        WCHAR exe[MAX_PATH * 2];
        JoinPath(exe, _countof(exe), candidates[i], L"SlayTheSpire2.exe");
        if (FileExistsW2(exe)) { wcscpy(g_gameDir, candidates[i]); return; }
    }
}

static void FindWorkshopDir(WCHAR* out, int cap) {
    WCHAR common[MAX_PATH * 2], steamapps[MAX_PATH * 2];
    wcscpy(common, g_gameDir); DirName(common);
    wcscpy(steamapps, common); DirName(steamapps);
    swprintf(out, cap, L"%ls\\workshop\\content\\2868840", steamapps);
}

static void FindSettingsFile(void) {
    WCHAR roaming[MAX_PATH];
    if (FAILED(SHGetFolderPathW(NULL, CSIDL_APPDATA, NULL, SHGFP_TYPE_CURRENT, roaming))) return;
    WCHAR root[MAX_PATH * 2];
    swprintf(root, _countof(root), L"%ls\\SlayTheSpire2", roaming);
    WCHAR search[MAX_PATH * 2];
    swprintf(search, _countof(search), L"%ls\\*", root);
    FILETIME newest = {0};
    typedef struct StackItem { WCHAR path[MAX_PATH * 2]; int depth; } StackItem;
    StackItem stack[128];
    int sp = 0;
    wcscpy(stack[sp].path, root);
    stack[sp].depth = 0;
    ++sp;

    while (sp > 0) {
        StackItem item = stack[--sp];
        swprintf(search, _countof(search), L"%ls\\*", item.path);
        WIN32_FIND_DATAW fd;
        HANDLE h = FindFirstFileW(search, &fd);
        if (h == INVALID_HANDLE_VALUE) continue;
        do {
            if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0) continue;
            WCHAR path[MAX_PATH * 2];
            swprintf(path, _countof(path), L"%ls\\%ls", item.path, fd.cFileName);
            if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
                if (item.depth < 6 && sp < 128) {
                    wcscpy(stack[sp].path, path);
                    stack[sp].depth = item.depth + 1;
                    ++sp;
                }
                continue;
            }

            if (_wcsicmp(fd.cFileName, L"settings.save") == 0) {
                WIN32_FILE_ATTRIBUTE_DATA ad;
                if (GetFileAttributesExW(path, GetFileExInfoStandard, &ad)) {
                    if (!g_settingsFile[0] || CompareFileTime(&ad.ftLastWriteTime, &newest) > 0) {
                        newest = ad.ftLastWriteTime;
                        wcscpy(g_settingsFile, path);
                    }
                }
            }
        } while (FindNextFileW(h, &fd));
        FindClose(h);
    }
}

static void DetectLanguage(void) {
    g_chinese = FALSE;
    DWORD size = 0;
    char* json = ReadFileBytes(g_settingsFile, &size);
    if (!json) return;
    WCHAR lang[64] = L"";
    JsonStringValue(json, "language", lang, _countof(lang));
    g_chinese = IsChineseLanguage(lang);
    HeapFree(GetProcessHeap(), 0, json);
}

static int HasModId(const WCHAR* id) {
    for (int i = 0; i < g_modCount; ++i) if (_wcsicmp(g_mods[i].id, id) == 0) return 1;
    return 0;
}

static void AddModFromJson(const WCHAR* path, const WCHAR* source) {
    if (g_modCount >= MAX_MODS) return;
    DWORD size = 0;
    char* json = ReadFileBytes(path, &size);
    if (!json) return;
    WCHAR id[MAX_TEXT] = L"", name[MAX_TEXT] = L"";
    if (JsonStringValue(json, "id", id, _countof(id)) && id[0] && !HasModId(id)) {
        JsonStringValue(json, "name", name, _countof(name));
        if (!name[0]) wcscpy(name, id);
        wcscpy(g_mods[g_modCount].id, id);
        wcscpy(g_mods[g_modCount].name, name);
        wcscpy(g_mods[g_modCount].source, source);
        wcsncpy(g_mods[g_modCount].manifest, path, _countof(g_mods[g_modCount].manifest) - 1);
        JsonDependencyIds(json, g_mods[g_modCount].deps, &g_mods[g_modCount].depCount);
        g_mods[g_modCount].affectsGameplay = JsonBoolValue(json, "affects_gameplay");
        ++g_modCount;
    }
    HeapFree(GetProcessHeap(), 0, json);
}

static void DiscoverRoot(const WCHAR* root, const WCHAR* source) {
    WCHAR search[MAX_PATH * 2];
    swprintf(search, _countof(search), L"%ls\\*", root);
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(search, &fd);
    if (h == INVALID_HANDLE_VALUE) {
        AppendLogf(L"Scan skipped: %ls root not found: %ls", source, root);
        return;
    }
    int before = g_modCount;
    do {
        if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0) continue;
        WCHAR path[MAX_PATH * 2];
        swprintf(path, _countof(path), L"%ls\\%ls", root, fd.cFileName);
        if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
            DiscoverRoot(path, source);
        } else {
            WCHAR* ext = wcsrchr(fd.cFileName, L'.');
            if (ext && _wcsicmp(ext, L".json") == 0) {
                AddModFromJson(path, source);
            }
        }
    } while (FindNextFileW(h, &fd));
    FindClose(h);
    AppendLogf(L"Scanned %ls root: %ls found=%d", source, root, g_modCount - before);
}

static int CmpMods(const void* a, const void* b) {
    return _wcsicmp(((const ModInfo*)a)->name, ((const ModInfo*)b)->name);
}

static int FindModIndexById(const WCHAR* id) {
    for (int i = 0; i < g_modCount; ++i) {
        if (_wcsicmp(g_mods[i].id, id) == 0) return i;
    }
    return -1;
}

static BOOL IdInSavedOrder(const WCHAR* id) {
    for (int i = 0; i < g_savedOrderCount; ++i) {
        if (_wcsicmp(g_savedOrder[i], id) == 0) return TRUE;
    }
    return FALSE;
}

static void GetLoadOrderPath(WCHAR* out, int cap) {
    WCHAR dataDir[MAX_PATH * 2];
    JoinPath(dataDir, _countof(dataDir), g_appDir, L"ModTheSpire2Data");
    CreateDirectoryW(dataDir, NULL);
    JoinPath(out, cap, dataDir, L"load-order.txt");
}

static void GetEnabledModsPath(WCHAR* out, int cap) {
    WCHAR dataDir[MAX_PATH * 2];
    JoinPath(dataDir, _countof(dataDir), g_appDir, L"ModTheSpire2Data");
    CreateDirectoryW(dataDir, NULL);
    JoinPath(out, cap, dataDir, L"enabled-mods.txt");
}

static void GetProfilesDir(WCHAR* out, int cap) {
    WCHAR dataDir[MAX_PATH * 2];
    JoinPath(dataDir, _countof(dataDir), g_appDir, L"ModTheSpire2Data");
    CreateDirectoryW(dataDir, NULL);
    JoinPath(out, cap, dataDir, L"order-profiles");
    CreateDirectoryW(out, NULL);
}

static void SanitizeProfileName(const WCHAR* input, WCHAR* out, int cap) {
    int j = 0;
    for (int i = 0; input && input[i] && j < cap - 1; ++i) {
        WCHAR c = input[i];
        if ((c >= L'a' && c <= L'z') || (c >= L'A' && c <= L'Z') || (c >= L'0' && c <= L'9') || c == L'-' || c == L'_' || c == L' ') {
            out[j++] = c;
        }
    }
    while (j > 0 && out[j - 1] == L' ') --j;
    out[j] = 0;
    if (!out[0]) wcsncpy(out, L"default", cap - 1);
    out[cap - 1] = 0;
}

static void GetProfilePath(const WCHAR* profileName, WCHAR* out, int cap) {
    WCHAR dir[MAX_PATH * 2], safe[MAX_PROFILE], file[MAX_PROFILE + 8];
    GetProfilesDir(dir, _countof(dir));
    SanitizeProfileName(profileName, safe, _countof(safe));
    swprintf(file, _countof(file), L"%ls.txt", safe);
    JoinPath(out, cap, dir, file);
}

static void LoadSavedOrder(void) {
    g_savedOrderCount = 0;
    WCHAR path[MAX_PATH * 2];
    GetLoadOrderPath(path, _countof(path));
    DWORD size = 0;
    char* data = ReadFileBytes(path, &size);
    if (!data) return;
    char* p = data;
    if ((unsigned char)p[0] == 0xEF && (unsigned char)p[1] == 0xBB && (unsigned char)p[2] == 0xBF) {
        p += 3;
    }
    while (*p && g_savedOrderCount < MAX_MODS) {
        while (*p == '\r' || *p == '\n' || *p == ' ' || *p == '\t') ++p;
        char* start = p;
        while (*p && *p != '\r' && *p != '\n') ++p;
        char* end = p;
        while (end > start && (end[-1] == ' ' || end[-1] == '\t')) --end;
        if (end > start) {
            Utf8ToWide2(start, (int)(end - start), g_savedOrder[g_savedOrderCount], MAX_TEXT);
            if (g_savedOrder[g_savedOrderCount][0]) ++g_savedOrderCount;
        }
    }
    HeapFree(GetProcessHeap(), 0, data);
    AppendLogf(L"Loaded custom order entries=%d", g_savedOrderCount);
}

static void LoadSavedEnabled(void) {
    g_savedEnabledCount = 0;
    WCHAR path[MAX_PATH * 2];
    GetEnabledModsPath(path, _countof(path));
    DWORD size = 0;
    char* data = ReadFileBytes(path, &size);
    if (!data) return;
    char* p = data;
    if ((unsigned char)p[0] == 0xEF && (unsigned char)p[1] == 0xBB && (unsigned char)p[2] == 0xBF) {
        p += 3;
    }
    while (*p && g_savedEnabledCount < MAX_MODS) {
        while (*p == '\r' || *p == '\n' || *p == ' ' || *p == '\t') ++p;
        char* start = p;
        while (*p && *p != '\r' && *p != '\n') ++p;
        char* end = p;
        while (end > start && (end[-1] == ' ' || end[-1] == '\t')) --end;
        if (end > start) {
            Utf8ToWide2(start, (int)(end - start), g_savedEnabled[g_savedEnabledCount], MAX_TEXT);
            if (g_savedEnabled[g_savedEnabledCount][0]) ++g_savedEnabledCount;
        }
    }
    HeapFree(GetProcessHeap(), 0, data);
    AppendLogf(L"Loaded saved enabled entries=%d", g_savedEnabledCount);
}

static BOOL IdInSavedEnabled(const WCHAR* id) {
    for (int i = 0; i < g_savedEnabledCount; ++i) {
        if (_wcsicmp(g_savedEnabled[i], id) == 0) return TRUE;
    }
    return FALSE;
}

static BOOL SaveCurrentEnabled(void) {
    WCHAR path[MAX_PATH * 2];
    GetEnabledModsPath(path, _countof(path));
    HANDLE h = CreateFileW(path, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE) {
        return FALSE;
    }

    for (int i = 0; i < g_modCount; ++i) {
        if (!ListView_GetCheckState(g_list, i)) continue;
        char id8[MAX_TEXT * 4];
        int len = WideToUtf82(g_mods[i].id, id8, sizeof(id8));
        if (len > 1) {
            DWORD written = 0;
            WriteFile(h, id8, (DWORD)(len - 1), &written, NULL);
            WriteFile(h, "\r\n", 2, &written, NULL);
        }
    }
    CloseHandle(h);
    LoadSavedEnabled();
    return TRUE;
}

static BOOL SaveOrderToPath(const WCHAR* path) {
    HANDLE h = CreateFileW(path, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE) {
        return FALSE;
    }

    for (int i = 0; i < g_modCount; ++i) {
        char id8[MAX_TEXT * 4];
        int len = WideToUtf82(g_mods[i].id, id8, sizeof(id8));
        if (len > 1) {
            DWORD written = 0;
            WriteFile(h, id8, (DWORD)(len - 1), &written, NULL);
            WriteFile(h, "\r\n", 2, &written, NULL);
        }
    }
    CloseHandle(h);
    return TRUE;
}

static void SaveCurrentOrder(void) {
    WCHAR path[MAX_PATH * 2];
    RepairDependencyOrderInPlace();
    RebuildListPreservingChecks(GetSelectedListIndex());
    GetLoadOrderPath(path, _countof(path));
    if (!SaveOrderToPath(path)) {
        MessageBoxW(NULL, L"Could not save load-order.txt.", L"ModTheSpire2", MB_ICONERROR);
        return;
    }
    if (!SaveCurrentEnabled()) {
        MessageBoxW(NULL, L"Could not save enabled-mods.txt.", L"ModTheSpire2", MB_ICONERROR);
        return;
    }
    LoadSavedOrder();
    SetWindowTextW(g_status, L"Custom load order and enabled mods saved.");
}

static int SavedOrderRank(const WCHAR* id) {
    for (int i = 0; i < g_savedOrderCount; ++i) {
        if (_wcsicmp(g_savedOrder[i], id) == 0) return i;
    }
    return 1000000;
}

static int CmpModsSavedOrder(const void* a, const void* b) {
    const ModInfo* ma = (const ModInfo*)a;
    const ModInfo* mb = (const ModInfo*)b;
    int ra = SavedOrderRank(ma->id);
    int rb = SavedOrderRank(mb->id);
    if (ra != rb) return ra - rb;
    return _wcsicmp(ma->name, mb->name);
}

static void ApplySavedOrderBeforeDependencySort(void) {
    if (g_savedOrderCount <= 0) return;
    qsort(g_mods, g_modCount, sizeof(ModInfo), CmpModsSavedOrder);
}

static int FirstKnownDepIndex(const ModInfo* mod) {
    for (int i = 0; i < mod->depCount; ++i) {
        int dep = FindModIndexById(mod->deps[i]);
        if (dep >= 0) return dep;
    }
    return -1;
}

static BOOL DependsOn(const ModInfo* mod, const WCHAR* depId) {
    for (int i = 0; i < mod->depCount; ++i) {
        if (_wcsicmp(mod->deps[i], depId) == 0) return TRUE;
    }
    return FALSE;
}

static BOOL ValidateDependencyOrder(WCHAR* outMessage, int cap) {
    for (int i = 0; i < g_modCount; ++i) {
        for (int d = 0; d < g_mods[i].depCount; ++d) {
            int depIndex = FindModIndexById(g_mods[i].deps[d]);
            if (depIndex >= 0 && depIndex > i) {
                if (outMessage && cap > 0) {
                    const WCHAR* modName = g_mods[i].name[0] ? g_mods[i].name : g_mods[i].id;
                    const WCHAR* depName = g_mods[depIndex].name[0] ? g_mods[depIndex].name : g_mods[depIndex].id;
                    swprintf(outMessage, cap, L"Invalid order: %ls must load before %ls.", depName, modName);
                }
                return FALSE;
            }
        }
    }
    if (outMessage && cap > 0) outMessage[0] = 0;
    return TRUE;
}

static void RepairDependencyOrderInPlace(void) {
    BOOL changed = TRUE;
    int guard = 0;
    while (changed && guard++ < MAX_MODS) {
        changed = FALSE;
        for (int i = 0; i < g_modCount; ++i) {
            for (int d = 0; d < g_mods[i].depCount; ++d) {
                int depIndex = FindModIndexById(g_mods[i].deps[d]);
                if (depIndex > i) {
                    ModInfo dep = g_mods[depIndex];
                    for (int j = depIndex; j > i; --j) {
                        g_mods[j] = g_mods[j - 1];
                    }
                    g_mods[i] = dep;
                    changed = TRUE;
                    break;
                }
            }
            if (changed) break;
        }
    }
}

static BOOL HasUncheckedKnownDependency(int index, WCHAR* outName, int cap) {
    if (index < 0 || index >= g_modCount) return FALSE;
    for (int d = 0; d < g_mods[index].depCount; ++d) {
        int dep = FindModIndexById(g_mods[index].deps[d]);
        if (dep >= 0 && !ListView_GetCheckState(g_list, dep)) {
            if (outName && cap > 0) {
                wcsncpy(outName, g_mods[dep].name[0] ? g_mods[dep].name : g_mods[dep].id, cap - 1);
                outName[cap - 1] = 0;
            }
            return TRUE;
        }
    }
    return FALSE;
}

static void UncheckDependentsRecursive(int dependencyIndex) {
    if (dependencyIndex < 0 || dependencyIndex >= g_modCount) return;
    for (int i = 0; i < g_modCount; ++i) {
        if (ListView_GetCheckState(g_list, i) && DependsOn(&g_mods[i], g_mods[dependencyIndex].id)) {
            ListView_SetCheckState(g_list, i, FALSE);
            UncheckDependentsRecursive(i);
        }
    }
}

static void EmitModTree(int index, int depth, BOOL* placed, ModInfo* sorted, int* out) {
    if (index < 0 || index >= g_modCount || placed[index]) return;
    ModInfo mod = g_mods[index];
    mod.depth = depth > 4 ? 4 : depth;
    sorted[(*out)++] = mod;
    placed[index] = TRUE;

    for (int i = 0; i < g_modCount; ++i) {
        if (!placed[i] && DependsOn(&g_mods[i], g_mods[index].id)) {
            EmitModTree(i, depth + 1, placed, sorted, out);
        }
    }
}

static void SortModsByDependencies(void) {
    if (g_savedOrderCount > 0) ApplySavedOrderBeforeDependencySort();
    else qsort(g_mods, g_modCount, sizeof(ModInfo), CmpMods);
    ModInfo* sorted = (ModInfo*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(ModInfo) * MAX_MODS);
    BOOL* placed = (BOOL*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(BOOL) * MAX_MODS);
    int out = 0;
    if (!sorted || !placed) {
        if (sorted) HeapFree(GetProcessHeap(), 0, sorted);
        if (placed) HeapFree(GetProcessHeap(), 0, placed);
        return;
    }

    for (int i = 0; i < g_modCount; ++i) {
        if (FirstKnownDepIndex(&g_mods[i]) < 0) {
            EmitModTree(i, 0, placed, sorted, &out);
        }
    }

    for (int i = 0; i < g_modCount; ++i) {
        if (!placed[i]) {
            EmitModTree(i, 0, placed, sorted, &out);
        }
    }

    for (int i = 0; i < g_modCount; ++i) {
        g_mods[i] = sorted[i];
    }

    HeapFree(GetProcessHeap(), 0, sorted);
    HeapFree(GetProcessHeap(), 0, placed);
}

static void BackupSettings(void) {
    WCHAR dir[MAX_PATH * 2], dst[MAX_PATH * 2], name[128];
    JoinPath(dir, _countof(dir), g_appDir, L"ModTheSpire2Data");
    CreateDirectoryW(dir, NULL);
    wcscat(dir, L"\\settings-backups");
    CreateDirectoryW(dir, NULL);
    SYSTEMTIME st; GetLocalTime(&st);
    swprintf(name, _countof(name), L"settings.save.%04d%02d%02d-%02d%02d%02d.bak", st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond);
    JoinPath(dst, _countof(dst), dir, name);
    CopyFileW(g_settingsFile, dst, TRUE);
}

static const WCHAR* SourceForSettings(const ModInfo* mod) {
    return _wcsicmp(mod->source, L"Workshop") == 0 ? L"steam_workshop" : L"mods_directory";
}

static BOOL AppendBytes(char** out, DWORD* len, DWORD* cap, const char* bytes, DWORD bytesLen) {
    if (*len + bytesLen + 4 >= *cap) {
        while (*len + bytesLen + 4 >= *cap) *cap *= 2;
        char* bigger = (char*)HeapReAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, *out, *cap);
        if (!bigger) return FALSE;
        *out = bigger;
    }
    memcpy(*out + *len, bytes, bytesLen);
    *len += bytesLen;
    (*out)[*len] = 0;
    return TRUE;
}

static char* BuildModListJson(BOOL modded, const char* oldList, DWORD oldListLen, DWORD* outLen) {
    DWORD cap = 4096 + (DWORD)g_modCount * 1024 + oldListLen;
    char* out = (char*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, cap);
    if (!out) return NULL;
    DWORD len = 0;
    BOOL needComma = FALSE;
    out[len++] = '[';

    for (int i = 0; i < g_modCount; ++i) {
        char id8[MAX_TEXT * 4];
        char source8[128];
        WideToUtf82(g_mods[i].id, id8, sizeof(id8));
        WideToUtf82(SourceForSettings(&g_mods[i]), source8, sizeof(source8));
        BOOL on = modded && ListView_GetCheckState(g_list, i);
        char entry[MAX_TEXT * 4 + 256];
        snprintf(entry, sizeof(entry), "%s{\"id\":\"%s\",\"is_enabled\":%s,\"source\":\"%s\"}",
                 needComma ? "," : "", id8, on ? "true" : "false", source8);
        DWORD entryLen = (DWORD)strlen(entry);
        if (!AppendBytes(&out, &len, &cap, entry, entryLen)) {
            HeapFree(GetProcessHeap(), 0, out);
            return NULL;
        }
        needComma = TRUE;
    }

    const char* p = oldList;
    const char* end = oldList + oldListLen;
    while (p < end) {
        if (*p != '{') {
            ++p;
            continue;
        }
        const char* objectStart = p;
        BOOL inString = FALSE, escaped = FALSE;
        int depth = 0;
        while (p < end) {
            char c = *p;
            if (inString) {
                if (escaped) escaped = FALSE;
                else if (c == '\\') escaped = TRUE;
                else if (c == '"') inString = FALSE;
            } else {
                if (c == '"') inString = TRUE;
                else if (c == '{') ++depth;
                else if (c == '}') {
                    --depth;
                    if (depth == 0) {
                        ++p;
                        break;
                    }
                }
            }
            ++p;
        }
        const char* objectEnd = p;
        if (objectEnd > objectStart && !JsonObjectIdInDiscoveredMods(objectStart, objectEnd)) {
            if (needComma && !AppendBytes(&out, &len, &cap, ",", 1)) {
                HeapFree(GetProcessHeap(), 0, out);
                return NULL;
            }
            if (!AppendBytes(&out, &len, &cap, objectStart, (DWORD)(objectEnd - objectStart))) {
                HeapFree(GetProcessHeap(), 0, out);
                return NULL;
            }
            needComma = TRUE;
        }
    }

    out[len++] = ']';
    out[len] = 0;
    *outLen = len;
    return out;
}

static char* ReplaceModList(char* json, DWORD* size, BOOL modded) {
    DWORD start = 0, len = 0, newLen = 0;
    if (!FindModListSpan(json, *size, &start, &len)) return json;
    char* modList = BuildModListJson(modded, json + start, len, &newLen);
    if (!modList) return json;
    char* replaced = ReplaceSpan(json, size, start, len, modList);
    HeapFree(GetProcessHeap(), 0, modList);
    return replaced;
}

static char* ReplaceAll(const char* src, DWORD* size, const char* oldText, const char* newText) {
    const char* p = strstr(src, oldText);
    if (!p) return NULL;
    DWORD oldLen = (DWORD)strlen(oldText), newLen = (DWORD)strlen(newText), srcLen = *size;
    DWORD outLen = srcLen - oldLen + newLen;
    char* out = (char*)HeapAlloc(GetProcessHeap(), 0, outLen + 1);
    DWORD prefix = (DWORD)(p - src);
    memcpy(out, src, prefix);
    memcpy(out + prefix, newText, newLen);
    memcpy(out + prefix + newLen, p + oldLen, srcLen - prefix - oldLen);
    out[outLen] = 0;
    *size = outLen;
    return out;
}

static char* ReplaceSpan(char* src, DWORD* size, DWORD start, DWORD oldLen, const char* newText) {
    DWORD newLen = (DWORD)strlen(newText);
    DWORD outLen = *size - oldLen + newLen;
    char* out = (char*)HeapAlloc(GetProcessHeap(), 0, outLen + 1);
    if (!out) return src;
    memcpy(out, src, start);
    memcpy(out + start, newText, newLen);
    memcpy(out + start + newLen, src + start + oldLen, *size - start - oldLen);
    out[outLen] = 0;
    HeapFree(GetProcessHeap(), 0, src);
    *size = outLen;
    return out;
}

static char* InsertMissingModEntry(char* json, DWORD* size, const WCHAR* id, BOOL on) {
    char id8[MAX_TEXT * 4];
    WideToUtf82(id, id8, sizeof(id8));
    char entry[MAX_TEXT * 4 + 96];
    snprintf(entry, sizeof(entry), "{\"id\":\"%s\",\"is_enabled\":%s,\"source\":\"mods_directory\"},", id8, on ? "true" : "false");
    char* p = strstr(json, "\"mod_list\"");
    if (!p) return json;
    p = strchr(p, '[');
    if (!p) return json;
    return ReplaceSpan(json, size, (DWORD)(p + 1 - json), 0, entry);
}

static void WriteSettings(BOOL modded) {
    if (!FileExistsW2(g_settingsFile)) {
        MessageBoxW(NULL, L"settings.save was not found. Start the game once first, or check the path.", L"ModTheSpire2", MB_ICONERROR);
        return;
    }
    BackupSettings();
    DWORD size = 0;
    char* json = ReadFileBytes(g_settingsFile, &size);
    if (!json) return;
    char* replaced = NULL;
    if (modded) replaced = ReplaceAll(json, &size, "\"mods_enabled\": false", "\"mods_enabled\": true");
    else replaced = ReplaceAll(json, &size, "\"mods_enabled\": true", "\"mods_enabled\": false");
    if (replaced) { HeapFree(GetProcessHeap(), 0, json); json = replaced; }

    json = ReplaceModList(json, &size, modded);
    WriteFileBytes(g_settingsFile, json, size);
    HeapFree(GetProcessHeap(), 0, json);
}

static void RefreshList(void) {
    ListView_DeleteAllItems(g_list);
    g_modCount = 0;
    LoadSavedOrder();
    LoadSavedEnabled();
    WCHAR root[MAX_PATH * 2], workshop[MAX_PATH * 2];
    swprintf(root, _countof(root), L"%ls\\mods", g_gameDir);
    AppendLogf(L"Refresh appDir=%ls", g_appDir);
    AppendLogf(L"Refresh gameDir=%ls", g_gameDir);
    AppendLogf(L"Refresh settings=%ls", g_settingsFile);
    DiscoverRoot(root, L"Local");
    FindWorkshopDir(workshop, _countof(workshop));
    DiscoverRoot(workshop, L"Workshop");
    SortModsByDependencies();
    AppendLogf(L"Refresh total mods=%d", g_modCount);
    DWORD size = 0;
    char* settings = ReadFileBytes(g_settingsFile, &size);
    g_enforcingChecks = TRUE;
    for (int i = 0; i < g_modCount; ++i) {
        WCHAR displayName[MAX_TEXT + 32];
        const WCHAR* indent = g_mods[i].depth == 0 ? L"" : (g_mods[i].depth == 1 ? L"    \u2514 " : L"        \u2514 ");
        swprintf(displayName, _countof(displayName), L"%ls%ls", indent, g_mods[i].name);
        LVITEMW item = {0};
        item.mask = LVIF_TEXT;
        item.iItem = i;
        item.pszText = displayName;
        ListView_InsertItem(g_list, &item);
        ListView_SetItemText(g_list, i, 1, g_mods[i].id);
        ListView_SetItemText(g_list, i, 2, g_mods[i].source);
        WCHAR typeText[32];
        wcsncpy(typeText, g_mods[i].affectsGameplay ? L"Gameplay" : L"Utility", _countof(typeText) - 1);
        typeText[_countof(typeText) - 1] = 0;
        ListView_SetItemText(g_list, i, 3, typeText);
        WCHAR orderText[32];
        swprintf(orderText, _countof(orderText), L"%d", i + 1);
        ListView_SetItemText(g_list, i, 4, orderText);
        WCHAR depText[MAX_TEXT] = L"";
        for (int d = 0; d < g_mods[i].depCount; ++d) {
            if (d > 0) wcscat(depText, L", ");
            wcsncat(depText, g_mods[i].deps[d], _countof(depText) - wcslen(depText) - 1);
        }
        ListView_SetItemText(g_list, i, 5, depText);
        if (g_savedEnabledCount > 0) {
            if (IdInSavedEnabled(g_mods[i].id)) ListView_SetCheckState(g_list, i, TRUE);
        } else if (settings && JsonBoolAfterId(settings, g_mods[i].id)) {
            ListView_SetCheckState(g_list, i, TRUE);
        }
    }
    for (int pass = 0; pass < MAX_DEPS; ++pass) {
        BOOL changed = FALSE;
        for (int i = 0; i < g_modCount; ++i) {
            if (ListView_GetCheckState(g_list, i) && HasUncheckedKnownDependency(i, NULL, 0)) {
                ListView_SetCheckState(g_list, i, FALSE);
                changed = TRUE;
            }
        }
        if (!changed) break;
    }
    g_enforcingChecks = FALSE;
    if (settings) HeapFree(GetProcessHeap(), 0, settings);
    WCHAR status[MAX_PATH * 2];
    if (g_modCount == 0) {
        swprintf(status, _countof(status), L"Found 0 mods. Game: %ls", g_gameDir);
    } else {
        swprintf(status, _countof(status), L"Found %d mods. Custom order entries: %d.", g_modCount, g_savedOrderCount);
    }
    SetWindowTextW(g_status, status);
}

static void QuoteArg(WCHAR* out, int cap, const WCHAR* arg) {
    if (!wcspbrk(arg, L" \t\"")) {
        wcsncpy(out, arg, cap - 1);
        out[cap - 1] = 0;
        return;
    }
    swprintf(out, cap, L"\"%ls\"", arg);
}

static int GetSelectedListIndex(void) {
    return ListView_GetNextItem(g_list, -1, LVNI_SELECTED);
}

static void SelectListIndex(int index) {
    if (index < 0 || index >= g_modCount) return;
    ListView_SetItemState(g_list, -1, 0, LVIS_SELECTED | LVIS_FOCUSED);
    ListView_SetItemState(g_list, index, LVIS_SELECTED | LVIS_FOCUSED, LVIS_SELECTED | LVIS_FOCUSED);
    ListView_EnsureVisible(g_list, index, FALSE);
}

static void RebuildListPreservingChecks(int selectedIndex) {
    WCHAR (*checked)[MAX_TEXT] = (WCHAR (*)[MAX_TEXT])HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(WCHAR) * MAX_MODS * MAX_TEXT);
    if (!checked) {
        SetWindowTextW(g_status, L"Could not rebuild list: out of memory.");
        return;
    }
    int checkedCount = 0;
    for (int i = 0; i < g_modCount && checkedCount < MAX_MODS; ++i) {
        if (ListView_GetCheckState(g_list, i)) {
            WCHAR id[MAX_TEXT] = L"";
            ListView_GetItemText(g_list, i, 1, id, _countof(id));
            if (!id[0]) {
                wcsncpy(id, g_mods[i].id, _countof(id) - 1);
                id[_countof(id) - 1] = 0;
            }
            wcsncpy(checked[checkedCount], id, MAX_TEXT - 1);
            checked[checkedCount][MAX_TEXT - 1] = 0;
            ++checkedCount;
        }
    }

    ListView_DeleteAllItems(g_list);
    g_enforcingChecks = TRUE;
    for (int i = 0; i < g_modCount; ++i) {
        WCHAR displayName[MAX_TEXT + 32];
        const WCHAR* indent = g_mods[i].depth == 0 ? L"" : (g_mods[i].depth == 1 ? L"    \x2514 " : L"        \x2514 ");
        swprintf(displayName, _countof(displayName), L"%ls%ls", indent, g_mods[i].name);
        LVITEMW item = {0};
        item.mask = LVIF_TEXT;
        item.iItem = i;
        item.pszText = displayName;
        ListView_InsertItem(g_list, &item);
        ListView_SetItemText(g_list, i, 1, g_mods[i].id);
        ListView_SetItemText(g_list, i, 2, g_mods[i].source);
        ListView_SetItemText(g_list, i, 3, g_mods[i].affectsGameplay ? L"Gameplay" : L"Utility");
        WCHAR orderText[32];
        swprintf(orderText, _countof(orderText), L"%d", i + 1);
        ListView_SetItemText(g_list, i, 4, orderText);
        WCHAR depText[MAX_TEXT] = L"";
        for (int d = 0; d < g_mods[i].depCount; ++d) {
            if (d > 0) wcscat(depText, L", ");
            wcsncat(depText, g_mods[i].deps[d], _countof(depText) - wcslen(depText) - 1);
        }
        ListView_SetItemText(g_list, i, 5, depText);
        for (int c = 0; c < checkedCount; ++c) {
            if (_wcsicmp(checked[c], g_mods[i].id) == 0) {
                ListView_SetCheckState(g_list, i, TRUE);
                break;
            }
        }
    }
    g_enforcingChecks = FALSE;
    SelectListIndex(selectedIndex);
    HeapFree(GetProcessHeap(), 0, checked);
}

static void MoveSelectedMod(int delta) {
    int index = GetSelectedListIndex();
    int target = index + delta;
    if (index < 0 || target < 0 || target >= g_modCount) return;

    if (delta > 0 && DependsOn(&g_mods[index], g_mods[target].id)) {
        SetWindowTextW(g_status, L"Cannot move a dependency below a mod that needs it.");
        return;
    }
    if (delta < 0 && DependsOn(&g_mods[target], g_mods[index].id)) {
        SetWindowTextW(g_status, L"Cannot move a mod above its dependency.");
        return;
    }

    ModInfo tmp = g_mods[index];
    g_mods[index] = g_mods[target];
    g_mods[target] = tmp;
    RebuildListPreservingChecks(target);
    SetWindowTextW(g_status, L"Order changed. Use Save Order to keep it for future launches.");
}

static BOOL TryMoveIndexForTest(int index, int delta) {
    int target = index + delta;
    if (index < 0 || index >= g_modCount || target < 0 || target >= g_modCount) return FALSE;
    if (delta > 0 && DependsOn(&g_mods[index], g_mods[target].id)) return FALSE;
    if (delta < 0 && DependsOn(&g_mods[target], g_mods[index].id)) return FALSE;
    ModInfo tmp = g_mods[index];
    g_mods[index] = g_mods[target];
    g_mods[target] = tmp;
    return TRUE;
}

static BOOL MoveModToIndex(int index, int target) {
    if (index < 0 || index >= g_modCount || target < 0 || target >= g_modCount) return FALSE;
    if (target == index) return TRUE;
    ModInfo moving = g_mods[index];
    if (target < index) {
        for (int i = index; i > target; --i) g_mods[i] = g_mods[i - 1];
    } else {
        for (int i = index; i < target; ++i) g_mods[i] = g_mods[i + 1];
    }
    g_mods[target] = moving;
    return TRUE;
}

static BOOL RunHiddenListMoveTest(void) {
    INITCOMMONCONTROLSEX icc = { sizeof(icc), ICC_LISTVIEW_CLASSES };
    InitCommonControlsEx(&icc);

    HWND host = CreateWindowW(L"STATIC", L"", WS_OVERLAPPED, 0, 0, 100, 100, NULL, NULL, g_instance, NULL);
    if (!host) {
        AppendLog(L"Order self-test failed: could not create hidden host window");
        return FALSE;
    }

    HWND oldList = g_list;
    HWND oldStatus = g_status;
    g_list = CreateWindowW(WC_LISTVIEWW, L"", WS_CHILD | LVS_REPORT | LVS_SINGLESEL, 0, 0, 100, 100, host, NULL, g_instance, NULL);
    g_status = CreateWindowW(L"STATIC", L"", WS_CHILD, 0, 0, 100, 20, host, NULL, g_instance, NULL);
    if (!g_list || !g_status) {
        AppendLog(L"Order self-test failed: could not create hidden list controls");
        if (g_list) DestroyWindow(g_list);
        if (g_status) DestroyWindow(g_status);
        DestroyWindow(host);
        g_list = oldList;
        g_status = oldStatus;
        return FALSE;
    }
    ListView_SetExtendedListViewStyle(g_list, LVS_EX_CHECKBOXES | LVS_EX_FULLROWSELECT);

    LVCOLUMNW col = {0};
    col.mask = LVCF_TEXT | LVCF_WIDTH;
    col.cx = 100; col.pszText = (LPWSTR)L"Name"; ListView_InsertColumn(g_list, 0, &col);
    col.cx = 100; col.pszText = (LPWSTR)L"ID"; ListView_InsertColumn(g_list, 1, &col);
    col.cx = 80; col.pszText = (LPWSTR)L"Source"; ListView_InsertColumn(g_list, 2, &col);
    col.cx = 80; col.pszText = (LPWSTR)L"Type"; ListView_InsertColumn(g_list, 3, &col);
    col.cx = 60; col.pszText = (LPWSTR)L"Order"; ListView_InsertColumn(g_list, 4, &col);
    col.cx = 100; col.pszText = (LPWSTR)L"Depends on"; ListView_InsertColumn(g_list, 5, &col);

    RebuildListPreservingChecks(0);
    int firstCount = ListView_GetItemCount(g_list);
    if (firstCount != g_modCount) {
        AppendLogf(L"Order self-test failed: hidden list initial count=%d expected=%d", firstCount, g_modCount);
        DestroyWindow(g_list);
        DestroyWindow(g_status);
        DestroyWindow(host);
        g_list = oldList;
        g_status = oldStatus;
        return FALSE;
    }

    WCHAR firstId[MAX_TEXT], lastId[MAX_TEXT];
    wcscpy(firstId, g_mods[0].id);
    wcscpy(lastId, g_mods[g_modCount - 1].id);
    SelectListIndex(0);
    MoveSelectedMod(-1);
    SelectListIndex(g_modCount - 1);
    MoveSelectedMod(1);
    if (_wcsicmp(firstId, g_mods[0].id) != 0 || _wcsicmp(lastId, g_mods[g_modCount - 1].id) != 0) {
        AppendLog(L"Order self-test failed: hidden list boundary button path changed order");
        DestroyWindow(g_list);
        DestroyWindow(g_status);
        DestroyWindow(host);
        g_list = oldList;
        g_status = oldStatus;
        return FALSE;
    }

    int independent = -1;
    for (int i = 1; i < g_modCount - 1; ++i) {
        if (g_mods[i].depCount == 0) {
            independent = i;
            break;
        }
    }
    if (independent >= 0) {
        WCHAR movingId[MAX_TEXT];
        wcscpy(movingId, g_mods[independent].id);
        for (int i = 0; i < g_modCount; ++i) {
            ListView_SetCheckState(g_list, i, FALSE);
        }
        ListView_SetCheckState(g_list, independent, TRUE);
        SelectListIndex(independent);
        MoveSelectedMod(-1);
        int moved = FindModIndexById(movingId);
        if (moved < 0 || moved >= independent || ListView_GetItemCount(g_list) != g_modCount) {
            AppendLog(L"Order self-test failed: hidden list independent Move Up path failed");
            DestroyWindow(g_list);
            DestroyWindow(g_status);
            DestroyWindow(host);
            g_list = oldList;
            g_status = oldStatus;
            return FALSE;
        }
        if (!ListView_GetCheckState(g_list, moved)) {
            AppendLog(L"Order self-test failed: hidden list checked state did not follow moved mod");
            DestroyWindow(g_list);
            DestroyWindow(g_status);
            DestroyWindow(host);
            g_list = oldList;
            g_status = oldStatus;
            return FALSE;
        }
        if (!SaveCurrentEnabled()) {
            AppendLog(L"Order self-test failed: could not save enabled-mods.txt");
            DestroyWindow(g_list);
            DestroyWindow(g_status);
            DestroyWindow(host);
            g_list = oldList;
            g_status = oldStatus;
            return FALSE;
        }
        WCHAR enabledPath[MAX_PATH * 2];
        GetEnabledModsPath(enabledPath, _countof(enabledPath));
        DWORD enabledSize = 0;
        char* enabledData = ReadFileBytes(enabledPath, &enabledSize);
        char movingId8[MAX_TEXT * 4];
        WideToUtf82(movingId, movingId8, sizeof(movingId8));
        if (!enabledData || !strstr(enabledData, movingId8)) {
            AppendLog(L"Order self-test failed: enabled-mods.txt did not include moved checked mod");
            if (enabledData) HeapFree(GetProcessHeap(), 0, enabledData);
            DestroyWindow(g_list);
            DestroyWindow(g_status);
            DestroyWindow(host);
            g_list = oldList;
            g_status = oldStatus;
            return FALSE;
        }
        HeapFree(GetProcessHeap(), 0, enabledData);
    }

    DestroyWindow(g_list);
    DestroyWindow(g_status);
    DestroyWindow(host);
    g_list = oldList;
    g_status = oldStatus;
    return TRUE;
}

static void ApplySelectedOrderNumber(void) {
    int index = GetSelectedListIndex();
    if (index < 0 || index >= g_modCount) {
        SetWindowTextW(g_status, L"Select a mod before applying an order number.");
        return;
    }
    WCHAR text[64];
    GetWindowTextW(g_orderEdit, text, _countof(text));
    WCHAR* end = NULL;
    long value = wcstol(text, &end, 10);
    if (value < 1) value = 1;
    if (value > g_modCount) value = g_modCount;
    int target = (int)value - 1;
    if (target == index) {
        SetWindowTextW(g_status, L"Order number unchanged.");
        return;
    }

    MoveModToIndex(index, target);
    RebuildListPreservingChecks(target);
    SetWindowTextW(g_status, L"Order number applied. Save Order to keep it.");
}

static void ResetSavedOrder(void) {
    WCHAR path[MAX_PATH * 2];
    GetLoadOrderPath(path, _countof(path));
    DeleteFileW(path);
    g_savedOrderCount = 0;
    RefreshList();
    SetWindowTextW(g_status, L"Custom load order reset.");
}

static void RefreshProfiles(void) {
    if (!g_profileCombo) return;
    SendMessageW(g_profileCombo, CB_RESETCONTENT, 0, 0);
    SendMessageW(g_profileCombo, CB_ADDSTRING, 0, (LPARAM)L"default");
    WCHAR dir[MAX_PATH * 2], search[MAX_PATH * 2];
    GetProfilesDir(dir, _countof(dir));
    swprintf(search, _countof(search), L"%ls\\*.txt", dir);
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(search, &fd);
    if (h != INVALID_HANDLE_VALUE) {
        do {
            WCHAR name[MAX_PROFILE];
            wcsncpy(name, fd.cFileName, _countof(name) - 1);
            name[_countof(name) - 1] = 0;
            WCHAR* dot = wcsrchr(name, L'.');
            if (dot) *dot = 0;
            if (name[0]) SendMessageW(g_profileCombo, CB_ADDSTRING, 0, (LPARAM)name);
        } while (FindNextFileW(h, &fd));
        FindClose(h);
    }
    SetWindowTextW(g_profileCombo, L"default");
}

static void GetCurrentProfileName(WCHAR* out, int cap) {
    GetWindowTextW(g_profileCombo, out, cap);
    if (!out[0]) wcsncpy(out, L"default", cap - 1);
    out[cap - 1] = 0;
}

static void SaveNamedProfile(void) {
    WCHAR profile[MAX_PROFILE], path[MAX_PATH * 2];
    GetCurrentProfileName(profile, _countof(profile));
    RepairDependencyOrderInPlace();
    RebuildListPreservingChecks(GetSelectedListIndex());
    GetProfilePath(profile, path, _countof(path));
    if (!SaveOrderToPath(path)) {
        MessageBoxW(NULL, L"Could not save profile.", L"ModTheSpire2", MB_ICONERROR);
        return;
    }
    RefreshProfiles();
    SetWindowTextW(g_profileCombo, profile);
    SetWindowTextW(g_status, L"Named order profile saved.");
}

static void LoadOrderFromPath(const WCHAR* path) {
    DWORD size = 0;
    char* data = ReadFileBytes(path, &size);
    if (!data) {
        SetWindowTextW(g_status, L"Profile was not found.");
        return;
    }
    WCHAR (*ids)[MAX_TEXT] = (WCHAR (*)[MAX_TEXT])HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(WCHAR) * MAX_MODS * MAX_TEXT);
    ModInfo* ordered = (ModInfo*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(ModInfo) * MAX_MODS);
    BOOL* used = (BOOL*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(BOOL) * MAX_MODS);
    if (!ids || !ordered || !used) {
        if (ids) HeapFree(GetProcessHeap(), 0, ids);
        if (ordered) HeapFree(GetProcessHeap(), 0, ordered);
        if (used) HeapFree(GetProcessHeap(), 0, used);
        HeapFree(GetProcessHeap(), 0, data);
        SetWindowTextW(g_status, L"Could not load profile: out of memory.");
        return;
    }
    int idCount = 0;
    char* p = data;
    if ((unsigned char)p[0] == 0xEF && (unsigned char)p[1] == 0xBB && (unsigned char)p[2] == 0xBF) p += 3;
    while (*p && idCount < MAX_MODS) {
        while (*p == '\r' || *p == '\n' || *p == ' ' || *p == '\t') ++p;
        char* start = p;
        while (*p && *p != '\r' && *p != '\n') ++p;
        char* end = p;
        while (end > start && (end[-1] == ' ' || end[-1] == '\t')) --end;
        if (end > start) {
            Utf8ToWide2(start, (int)(end - start), ids[idCount], MAX_TEXT);
            if (ids[idCount][0]) ++idCount;
        }
    }
    HeapFree(GetProcessHeap(), 0, data);

    int out = 0;
    for (int i = 0; i < idCount; ++i) {
        for (int m = 0; m < g_modCount; ++m) {
            if (!used[m] && _wcsicmp(g_mods[m].id, ids[i]) == 0) {
                ordered[out++] = g_mods[m];
                used[m] = TRUE;
                break;
            }
        }
    }
    for (int m = 0; m < g_modCount; ++m) {
        if (!used[m]) ordered[out++] = g_mods[m];
    }
    for (int i = 0; i < g_modCount; ++i) g_mods[i] = ordered[i];
    HeapFree(GetProcessHeap(), 0, ids);
    HeapFree(GetProcessHeap(), 0, ordered);
    HeapFree(GetProcessHeap(), 0, used);
    RepairDependencyOrderInPlace();
    RebuildListPreservingChecks(0);
    SetWindowTextW(g_status, L"Named order profile loaded. Save Order to make it default.");
}

static void LoadNamedProfile(void) {
    WCHAR profile[MAX_PROFILE], path[MAX_PATH * 2];
    GetCurrentProfileName(profile, _countof(profile));
    if (_wcsicmp(profile, L"default") == 0) GetLoadOrderPath(path, _countof(path));
    else GetProfilePath(profile, path, _countof(path));
    LoadOrderFromPath(path);
}

static BOOL StartOriginalCommand(void) {
    if (!g_steamExe[0]) return FALSE;

    AppendLogf(L"Start original command: exe=%ls args=%ls", g_steamExe, g_steamArgs);
    SHELLEXECUTEINFOW info = {0};
    info.cbSize = sizeof(info);
    info.lpFile = g_steamExe;
    info.lpParameters = g_steamArgs[0] ? g_steamArgs : NULL;
    info.lpDirectory = g_gameDir[0] ? g_gameDir : NULL;
    info.nShow = SW_SHOWNORMAL;
    if (ShellExecuteExW(&info)) return TRUE;

    AppendLogf(L"ShellExecuteEx original command failed: %lu", GetLastError());
    return FALSE;
}

static BOOL StartGame(void) {
    if (StartOriginalCommand()) return TRUE;

    WCHAR exe[MAX_PATH * 2];
    JoinPath(exe, _countof(exe), g_gameDir, L"SlayTheSpire2.exe");
    if (!FileExistsW2(exe)) {
        MessageBoxW(NULL, L"SlayTheSpire2.exe was not found. Please check the game directory.", L"ModTheSpire2", MB_ICONERROR);
        return FALSE;
    }
    AppendLogf(L"Start fallback game exe by ShellExecute: %ls", exe);
    HINSTANCE result = ShellExecuteW(NULL, L"open", exe, NULL, g_gameDir, SW_SHOWNORMAL);
    if ((INT_PTR)result <= 32) {
        AppendLogf(L"ShellExecute fallback failed: %ld", (LONG_PTR)result);
        return FALSE;
    }
    return TRUE;
}

static void Launch(BOOL modded) {
    GetWindowTextW(g_gamePath, g_gameDir, _countof(g_gameDir));
    GetWindowTextW(g_settingsPath, g_settingsFile, _countof(g_settingsFile));
    AppendLogf(L"Launch requested modded=%d gameDir=%ls settings=%ls", modded, g_gameDir, g_settingsFile);
    if (modded) {
        BOOL any = FALSE;
        for (int i = 0; i < g_modCount; ++i) if (ListView_GetCheckState(g_list, i)) any = TRUE;
        AppendLogf(L"Launch selected any=%d", any);
        if (!any && MessageBoxW(NULL, L"No mods are selected. Launch vanilla instead?", L"ModTheSpire2", MB_YESNO | MB_ICONQUESTION) != IDYES) {
            AppendLog(L"Launch canceled: no mods selected");
            return;
        }
        if (!any) modded = FALSE;
        if (modded) {
            for (int i = 0; i < g_modCount; ++i) {
                WCHAR depName[MAX_TEXT];
                if (ListView_GetCheckState(g_list, i) && HasUncheckedKnownDependency(i, depName, _countof(depName))) {
                    WCHAR msg[MAX_TEXT * 2];
                    swprintf(msg, _countof(msg),
                             L"Select the required dependency first: %ls",
                             depName);
                    MessageBoxW(NULL, msg, L"ModTheSpire2", MB_OK | MB_ICONINFORMATION);
                    return;
                }
            }
        }
    }
    WCHAR orderMessage[MAX_TEXT * 2];
    if (!ValidateDependencyOrder(orderMessage, _countof(orderMessage))) {
        MessageBoxW(NULL, orderMessage, L"ModTheSpire2", MB_OK | MB_ICONINFORMATION);
        return;
    }
    if (modded && !SaveCurrentEnabled()) {
        MessageBoxW(NULL, L"Could not save enabled-mods.txt.", L"ModTheSpire2", MB_ICONERROR);
        return;
    }
    WriteSettings(modded);
    if (StartGame()) {
        PostQuitMessage(0);
    } else {
        MessageBoxW(NULL, L"Failed to start the game. Please check launcher.log.", L"ModTheSpire2", MB_ICONERROR);
    }
}

static void EnforceDependencyChecks(int changedIndex) {
    if (g_enforcingChecks) return;
    if (changedIndex < 0 || changedIndex >= g_modCount) return;
    g_enforcingChecks = TRUE;
    BOOL checked = ListView_GetCheckState(g_list, changedIndex);

    if (checked) {
        WCHAR depName[MAX_TEXT];
        if (HasUncheckedKnownDependency(changedIndex, depName, _countof(depName))) {
            ListView_SetCheckState(g_list, changedIndex, FALSE);
            WCHAR msg[MAX_TEXT * 2];
            swprintf(msg, _countof(msg),
                     L"Select the required dependency first: %ls",
                     depName);
            MessageBoxW(NULL, msg, L"ModTheSpire2", MB_OK | MB_ICONINFORMATION);
        }
    } else {
        UncheckDependentsRecursive(changedIndex);
    }
    g_enforcingChecks = FALSE;
}

static void AddColumns(void) {
    LVCOLUMNW col = {0};
    col.mask = LVCF_TEXT | LVCF_WIDTH;
    col.cx = 330; col.pszText = (LPWSTR)L"Name"; ListView_InsertColumn(g_list, 0, &col);
    col.cx = 210; col.pszText = L"ID"; ListView_InsertColumn(g_list, 1, &col);
    col.cx = 100; col.pszText = (LPWSTR)L"Source"; ListView_InsertColumn(g_list, 2, &col);
    col.cx = 85; col.pszText = (LPWSTR)L"Type"; ListView_InsertColumn(g_list, 3, &col);
    col.cx = 60; col.pszText = (LPWSTR)L"Order"; ListView_InsertColumn(g_list, 4, &col);
    col.cx = 170; col.pszText = (LPWSTR)L"Depends on"; ListView_InsertColumn(g_list, 5, &col);
}

static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp) {
    switch (msg) {
    case WM_CREATE:
        g_title = CreateWindowW(L"STATIC", L"ModTheSpire2", WS_CHILD | WS_VISIBLE, 18, 14, 260, 30, hwnd, NULL, g_instance, NULL);
        g_subtitle = CreateWindowW(L"STATIC", L"Choose mods for this launch. Use Move Up/Down to customize load order; dependencies must stay before dependents.", WS_CHILD | WS_VISIBLE, 20, 44, 940, 24, hwnd, NULL, g_instance, NULL);
        CreateWindowW(L"STATIC", L"Game", WS_CHILD | WS_VISIBLE, 20, 82, 90, 22, hwnd, NULL, g_instance, NULL);
        g_gamePath = CreateWindowW(L"EDIT", g_gameDir, WS_CHILD | WS_VISIBLE | WS_BORDER | ES_AUTOHSCROLL | ES_READONLY, 118, 78, 860, 26, hwnd, NULL, g_instance, NULL);
        CreateWindowW(L"STATIC", L"settings.save", WS_CHILD | WS_VISIBLE, 20, 116, 100, 22, hwnd, NULL, g_instance, NULL);
        g_settingsPath = CreateWindowW(L"EDIT", g_settingsFile, WS_CHILD | WS_VISIBLE | WS_BORDER | ES_AUTOHSCROLL | ES_READONLY, 118, 112, 860, 26, hwnd, NULL, g_instance, NULL);
        g_list = CreateWindowW(WC_LISTVIEWW, L"", WS_CHILD | WS_VISIBLE | WS_BORDER | LVS_REPORT | LVS_SINGLESEL, 20, 152, 958, 410, hwnd, NULL, g_instance, NULL);
        ListView_SetExtendedListViewStyle(g_list, LVS_EX_CHECKBOXES | LVS_EX_FULLROWSELECT | LVS_EX_DOUBLEBUFFER);
        ListView_SetBkColor(g_list, RGB(255, 253, 247));
        ListView_SetTextBkColor(g_list, RGB(255, 253, 247));
        ListView_SetTextColor(g_list, g_textColor);
        AddColumns();
        g_btnUp = CreateWindowW(L"BUTTON", L"Move Up", WS_CHILD | WS_VISIBLE, 20, 574, 92, 30, hwnd, (HMENU)103, g_instance, NULL);
        g_btnDown = CreateWindowW(L"BUTTON", L"Move Down", WS_CHILD | WS_VISIBLE, 124, 574, 104, 30, hwnd, (HMENU)104, g_instance, NULL);
        g_btnSaveOrder = CreateWindowW(L"BUTTON", L"Save Order", WS_CHILD | WS_VISIBLE, 240, 574, 112, 30, hwnd, (HMENU)105, g_instance, NULL);
        g_btnResetOrder = CreateWindowW(L"BUTTON", L"Reset Order", WS_CHILD | WS_VISIBLE, 364, 574, 112, 30, hwnd, (HMENU)106, g_instance, NULL);
        CreateWindowW(L"STATIC", L"Order", WS_CHILD | WS_VISIBLE, 500, 579, 46, 22, hwnd, NULL, g_instance, NULL);
        g_orderEdit = CreateWindowW(L"EDIT", L"1", WS_CHILD | WS_VISIBLE | WS_BORDER | ES_NUMBER, 548, 574, 54, 28, hwnd, (HMENU)107, g_instance, NULL);
        g_btnApplyOrder = CreateWindowW(L"BUTTON", L"Apply", WS_CHILD | WS_VISIBLE, 612, 574, 72, 30, hwnd, (HMENU)108, g_instance, NULL);
        CreateWindowW(L"STATIC", L"Profile", WS_CHILD | WS_VISIBLE, 704, 579, 52, 22, hwnd, NULL, g_instance, NULL);
        g_profileCombo = CreateWindowW(WC_COMBOBOXW, L"", WS_CHILD | WS_VISIBLE | CBS_DROPDOWN | WS_VSCROLL, 760, 574, 120, 120, hwnd, (HMENU)109, g_instance, NULL);
        g_btnSaveProfile = CreateWindowW(L"BUTTON", L"Save", WS_CHILD | WS_VISIBLE, 888, 574, 44, 30, hwnd, (HMENU)110, g_instance, NULL);
        g_btnLoadProfile = CreateWindowW(L"BUTTON", L"Load", WS_CHILD | WS_VISIBLE, 936, 574, 44, 30, hwnd, (HMENU)111, g_instance, NULL);
        g_btnRefresh = CreateWindowW(L"BUTTON", L"Refresh", WS_CHILD | WS_VISIBLE, 20, 614, 92, 34, hwnd, (HMENU)100, g_instance, NULL);
        g_btnVanilla = CreateWindowW(L"BUTTON", L"Vanilla", WS_CHILD | WS_VISIBLE, 124, 614, 120, 34, hwnd, (HMENU)101, g_instance, NULL);
        g_btnLaunch = CreateWindowW(L"BUTTON", L"Launch Selected", WS_CHILD | WS_VISIBLE, 256, 614, 174, 34, hwnd, (HMENU)102, g_instance, NULL);
        g_status = CreateWindowW(L"STATIC", L"", WS_CHILD | WS_VISIBLE, 448, 622, 530, 24, hwnd, NULL, g_instance, NULL);
        if (g_font) {
            SendMessageW(hwnd, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_subtitle, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_gamePath, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_settingsPath, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_list, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_status, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_btnUp, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_btnDown, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_btnSaveOrder, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_btnResetOrder, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_orderEdit, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_btnApplyOrder, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_profileCombo, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_btnSaveProfile, WM_SETFONT, (WPARAM)g_font, TRUE);
            SendMessageW(g_btnLoadProfile, WM_SETFONT, (WPARAM)g_font, TRUE);
        }
        if (g_titleFont) SendMessageW(g_title, WM_SETFONT, (WPARAM)g_titleFont, TRUE);
        RefreshProfiles();
        RefreshList();
        break;
    case WM_CTLCOLORSTATIC:
    case WM_CTLCOLOREDIT:
        SetTextColor((HDC)wp, g_textColor);
        SetBkColor((HDC)wp, g_bgColor);
        return (LRESULT)g_bgBrush;
    case WM_COMMAND:
        AppendLogf(L"WM_COMMAND id=%u code=%u hwnd=%p", LOWORD(wp), HIWORD(wp), (void*)lp);
        if (LOWORD(wp) == 100 || (HWND)lp == g_btnRefresh) RefreshList();
        if (LOWORD(wp) == 101 || (HWND)lp == g_btnVanilla) Launch(FALSE);
        if (LOWORD(wp) == 102 || (HWND)lp == g_btnLaunch) Launch(TRUE);
        if (LOWORD(wp) == 103 || (HWND)lp == g_btnUp) MoveSelectedMod(-1);
        if (LOWORD(wp) == 104 || (HWND)lp == g_btnDown) MoveSelectedMod(1);
        if (LOWORD(wp) == 105 || (HWND)lp == g_btnSaveOrder) SaveCurrentOrder();
        if (LOWORD(wp) == 106 || (HWND)lp == g_btnResetOrder) ResetSavedOrder();
        if (LOWORD(wp) == 108 || (HWND)lp == g_btnApplyOrder) ApplySelectedOrderNumber();
        if (LOWORD(wp) == 110 || (HWND)lp == g_btnSaveProfile) SaveNamedProfile();
        if (LOWORD(wp) == 111 || (HWND)lp == g_btnLoadProfile) LoadNamedProfile();
        break;
    case WM_NOTIFY:
        if (((LPNMHDR)lp)->hwndFrom == g_list && ((LPNMHDR)lp)->code == LVN_ITEMCHANGED) {
            NMLISTVIEW* nmlv = (NMLISTVIEW*)lp;
            if ((nmlv->uChanged & LVIF_STATE) && (nmlv->uNewState & LVIS_SELECTED) && g_orderEdit && nmlv->iItem >= 0) {
                WCHAR orderText[32];
                swprintf(orderText, _countof(orderText), L"%d", nmlv->iItem + 1);
                SetWindowTextW(g_orderEdit, orderText);
            }
            if ((nmlv->uChanged & LVIF_STATE) &&
                ((nmlv->uOldState ^ nmlv->uNewState) & LVIS_STATEIMAGEMASK)) {
                EnforceDependencyChecks(nmlv->iItem);
            }
        }
        break;
    case WM_DESTROY:
        if (g_bgBrush) DeleteObject(g_bgBrush);
        if (g_font) DeleteObject(g_font);
        if (g_titleFont) DeleteObject(g_titleFont);
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProcW(hwnd, msg, wp, lp);
    }
    return 0;
}

static void ParseArgs(LPWSTR cmdLine) {
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(cmdLine, &argc);
    BOOL pass = FALSE;
    for (int i = 0; i < argc; ++i) {
        if (!pass && wcscmp(argv[i], L"--") == 0) { pass = TRUE; continue; }
        if (!pass && wcscmp(argv[i], L"--diagnose") == 0) { g_diagnose = TRUE; continue; }
        if (!pass && wcscmp(argv[i], L"--self-test-order") == 0) { g_selfTestOrder = TRUE; continue; }
        if (!pass && wcscmp(argv[i], L"--wait-for-pid") == 0 && i + 1 < argc) { g_waitForPid = wcstoul(argv[++i], NULL, 10); continue; }
        if (pass) {
            if (!g_steamExe[0]) wcscpy(g_steamExe, argv[i]);
            else {
                WCHAR q[1024];
                QuoteArg(q, _countof(q), argv[i]);
                if (g_steamArgs[0]) wcscat(g_steamArgs, L" ");
                wcscat(g_steamArgs, q);
            }
        }
    }
    if (argv) LocalFree(argv);
}

static int RunDiagnostics(void) {
    g_modCount = 0;
    LoadSavedOrder();
    WCHAR localRoot[MAX_PATH * 2], workshopRoot[MAX_PATH * 2];
    swprintf(localRoot, _countof(localRoot), L"%ls\\mods", g_gameDir);
    FindWorkshopDir(workshopRoot, _countof(workshopRoot));

    AppendLog(L"Diagnostic scan begin");
    AppendLogf(L"Diagnostic appDir=%ls", g_appDir);
    AppendLogf(L"Diagnostic steamExe=%ls", g_steamExe);
    AppendLogf(L"Diagnostic gameDir=%ls", g_gameDir);
    AppendLogf(L"Diagnostic settings=%ls", g_settingsFile);
    AppendLogf(L"Diagnostic localRoot=%ls", localRoot);
    AppendLogf(L"Diagnostic workshopRoot=%ls", workshopRoot);

    DiscoverRoot(localRoot, L"Local");
    DiscoverRoot(workshopRoot, L"Workshop");
    SortModsByDependencies();

    AppendLogf(L"Diagnostic total mods=%d", g_modCount);
    for (int i = 0; i < g_modCount && i < 80; ++i) {
        WCHAR depText[MAX_TEXT] = L"";
        for (int d = 0; d < g_mods[i].depCount; ++d) {
            if (d > 0) wcscat(depText, L", ");
            wcsncat(depText, g_mods[i].deps[d], _countof(depText) - wcslen(depText) - 1);
        }
        AppendLogf(L"Diagnostic mod[%d]=%ls id=%ls source=%ls depth=%d deps=%ls", i, g_mods[i].name, g_mods[i].id, g_mods[i].source, g_mods[i].depth, depText);
    }
    AppendLog(L"Diagnostic scan end");

    WCHAR message[MAX_PATH * 6];
    swprintf(message, _countof(message),
             L"ModTheSpire2 diagnostic complete.\n\n"
             L"Mods found: %d\n\n"
             L"App dir:\n%ls\n\n"
             L"Steam command exe:\n%ls\n\n"
             L"Game dir:\n%ls\n\n"
             L"Local root:\n%ls\n\n"
             L"Workshop root:\n%ls\n\n"
             L"Logs are written beside the launcher, or to %%TEMP%%\\ModTheSpire2Launcher.log if that folder is not writable.",
             g_modCount, g_appDir, g_steamExe, g_gameDir, localRoot, workshopRoot);
    MessageBoxW(NULL, message, L"ModTheSpire2 Diagnostics", MB_OK | MB_ICONINFORMATION);
    return g_modCount > 0 ? 0 : 2;
}

static int RunOrderSelfTest(void) {
    g_modCount = 0;
    LoadSavedOrder();
    WCHAR localRoot[MAX_PATH * 2], workshopRoot[MAX_PATH * 2];
    swprintf(localRoot, _countof(localRoot), L"%ls\\mods", g_gameDir);
    FindWorkshopDir(workshopRoot, _countof(workshopRoot));
    AppendLog(L"Order self-test begin");
    DiscoverRoot(localRoot, L"Local");
    DiscoverRoot(workshopRoot, L"Workshop");
    SortModsByDependencies();
    AppendLogf(L"Order self-test mods=%d", g_modCount);
    if (g_modCount < 2) {
        AppendLog(L"Order self-test failed: not enough mods");
        return 2;
    }

    if (!RunHiddenListMoveTest()) {
        return 16;
    }

    WCHAR firstId[MAX_TEXT], lastId[MAX_TEXT];
    wcscpy(firstId, g_mods[0].id);
    wcscpy(lastId, g_mods[g_modCount - 1].id);
    if (TryMoveIndexForTest(0, -1)) {
        AppendLog(L"Order self-test failed: first item moved up");
        return 3;
    }
    if (_wcsicmp(firstId, g_mods[0].id) != 0) {
        AppendLog(L"Order self-test failed: first item changed after boundary move");
        return 4;
    }
    if (TryMoveIndexForTest(g_modCount - 1, 1)) {
        AppendLog(L"Order self-test failed: last item moved down");
        return 5;
    }
    if (_wcsicmp(lastId, g_mods[g_modCount - 1].id) != 0) {
        AppendLog(L"Order self-test failed: last item changed after boundary move");
        return 6;
    }

    int independent = -1;
    for (int i = 1; i < g_modCount - 1; ++i) {
        if (g_mods[i].depCount == 0) {
            independent = i;
            break;
        }
    }
    if (independent >= 0) {
        WCHAR movingId[MAX_TEXT];
        wcscpy(movingId, g_mods[independent].id);
        TryMoveIndexForTest(independent, -1);
        int moved = FindModIndexById(movingId);
        if (moved < 0 || moved >= independent) {
            AppendLog(L"Order self-test failed: independent move up did not move");
            return 7;
        }
        TryMoveIndexForTest(moved, 1);
        moved = FindModIndexById(movingId);
        if (moved != independent) {
            AppendLog(L"Order self-test failed: independent move down did not restore");
            return 8;
        }
    }

    independent = -1;
    for (int i = 0; i < g_modCount; ++i) {
        if (g_mods[i].depCount == 0) {
            independent = i;
            break;
        }
    }
    if (independent >= 0) {
        WCHAR movedId[MAX_TEXT];
        wcscpy(movedId, g_mods[independent].id);
        MoveModToIndex(independent, g_modCount - 1);
        int moved = FindModIndexById(movedId);
        if (moved != g_modCount - 1) {
            AppendLog(L"Order self-test failed: numeric move to last did not land at last");
            return 11;
        }
        MoveModToIndex(moved, 0);
        moved = FindModIndexById(movedId);
        if (moved != 0) {
            AppendLog(L"Order self-test failed: numeric move to first did not land at first");
            return 12;
        }
        RepairDependencyOrderInPlace();
        WCHAR repairMessage[MAX_TEXT * 2];
        if (!ValidateDependencyOrder(repairMessage, _countof(repairMessage))) {
            AppendLogf(L"Order self-test failed after repair: %ls", repairMessage);
            return 13;
        }
    }

    int base = FindModIndexById(L"BaseLib");
    int quick = FindModIndexById(L"QuickRestart");
    if (base >= 0 && quick >= 0) {
        if (base > quick) {
            AppendLog(L"Order self-test failed: BaseLib after QuickRestart");
            return 9;
        }
        if (quick - base > 1) {
            AppendLog(L"Order self-test info: independent mod may exist between BaseLib and QuickRestart; dependency order is still valid");
        }
    }

    WCHAR profilePath[MAX_PATH * 2];
    GetProfilePath(L"self-test", profilePath, _countof(profilePath));
    if (!SaveOrderToPath(profilePath)) {
        AppendLog(L"Order self-test failed: could not save self-test profile");
        return 14;
    }
    if (!FileExistsW2(profilePath)) {
        AppendLog(L"Order self-test failed: self-test profile missing after save");
        return 15;
    }
    DeleteFileW(profilePath);

    WCHAR message[MAX_TEXT * 2];
    if (!ValidateDependencyOrder(message, _countof(message))) {
        AppendLogf(L"Order self-test failed: %ls", message);
        return 10;
    }
    AppendLog(L"Order self-test passed");
    return 0;
}

static void WaitForPreviousGame(void) {
    if (!g_waitForPid) return;
    HANDLE h = OpenProcess(SYNCHRONIZE, FALSE, g_waitForPid);
    if (h) {
        WaitForSingleObject(h, 30000);
        CloseHandle(h);
        Sleep(800);
    }
}

int APIENTRY wWinMain(HINSTANCE instance, HINSTANCE prev, LPWSTR cmdLine, int show) {
    (void)prev;
    (void)cmdLine;
    g_instance = instance;
    GetModuleFileNameW(NULL, g_appDir, _countof(g_appDir));
    DirName(g_appDir);
    LPWSTR fullCommandLine = GetCommandLineW();
    if (fullCommandLine && wcsstr(fullCommandLine, L"--diagnose")) g_diagnose = TRUE;
    AppendLog(L"Boot");
    ParseArgs(fullCommandLine);
    AppendLogf(L"Launcher start. appDir=%ls waitForPid=%lu steamExe=%ls", g_appDir, g_waitForPid, g_steamExe);
    FindGameDir();
    FindSettingsFile();
    WaitForPreviousGame();
    DetectLanguage();
    AppendLogf(L"Detected gameDir=%ls", g_gameDir);
    AppendLogf(L"Detected settings=%ls", g_settingsFile);

    if (g_diagnose) {
        return RunDiagnostics();
    }
    if (g_selfTestOrder) {
        return RunOrderSelfTest();
    }

    INITCOMMONCONTROLSEX icc = { sizeof(icc), ICC_LISTVIEW_CLASSES };
    InitCommonControlsEx(&icc);
    WNDCLASSW wc = {0};
    wc.lpfnWndProc = WndProc;
    wc.hInstance = instance;
    wc.lpszClassName = L"ModTheSpire2LauncherWindow";
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    g_bgBrush = CreateSolidBrush(g_bgColor);
    wc.hbrBackground = g_bgBrush ? g_bgBrush : (HBRUSH)(COLOR_WINDOW + 1);
    RegisterClassW(&wc);
    g_font = CreateFontW(-16, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Segoe UI");
    g_titleFont = CreateFontW(-24, 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Segoe UI");
    HWND hwnd = CreateWindowW(wc.lpszClassName, L"ModTheSpire2", WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
                              CW_USEDEFAULT, CW_USEDEFAULT, 1015, 690, NULL, NULL, instance, NULL);
    ShowWindow(hwnd, show);
    UpdateWindow(hwnd);
    MSG msg;
    while (GetMessageW(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
    return 0;
}
