# AutoDNS

一鍵切換/測試/自動化管理 Windows 的 DNS。支援指定網路介面、DHCP、IPv4/IPv6，一鍵清快取、量測解析延遲，並可依執行中的程式自動切換到對應的 DNS。


---

## 特色

* ✅ 一鍵套用 DNS：AdGuard / HiNet / Cloudflare / Google 或切回 DHCP
* ✅ 多介面選取：乙太網路、Wi‑Fi（可選進階/虛擬/撥接）
* ✅ IPv6 自動啟用：未啟用時自動打開綁定
* ✅ 解析延遲量測：內建 PowerShell 腳本，多網域 × 多 DNS 比較，最低值會標記 `*`
* ✅ 自動切換 DNS：監控指定 .exe，對應不同 DNS；支援拖曳調整優先順序
* ✅ 深色 UI、固定視窗大小、可顯示/收合紀錄面板（最多保留 1000 行）

---

## 適合誰

* 想在**一般網站**過濾廣告的人（透過 **AdGuard DNS**）。

  > **注意**：DNS 階層無法穩定過濾 **YouTube** 內嵌廣告；此功能主要對一般網頁廣告有效，並非 100% 阻擋。
* 需要一鍵切換/比對 **HiNet / Cloudflare / Google / DHCP** 等 DNS 的使用者。
* 想讓特定程式（如 Steam、瀏覽器、下載工具）啟動時，自動切換到指定 DNS 的使用者。
* 玩線上遊戲時自動使用低延遲 DNS 解析連線初期反應時間

---

## 系統需求

* Windows 10/11（需可執行 `PowerShell` 與 `netsh`）
* .NET 8（目標框架：`net8.0-windows`）
* 系統管理員權限

---

## 安裝與執行

1. 建置或下載可執行檔，確保同資料夾可存取 `exePath.json`（若不存在將自動新增）。
2. **以系統管理員身分**執行 `AutoDNS.exe`（首次啟動會自動要求提權）。
3. 進入主畫面後即可依下方教學操作。

### Windows SmartScreen 檢測（未簽章）

此應用程式**未購買程式碼簽章憑證**，第一次執行時 Windows 可能跳出 SmartScreen 警告。

確認執行檔來源可信（可自行編譯或直接在Release頁面下載）：

1. 在 SmartScreen 視窗點擊 **「其他資訊」**。
2. 點擊 **「仍要執行」**（Run anyway）。
3. 等待 **Windows Defender** 掃描完成(10秒左右)。
4. 隨後的 UAC 提權請選擇 **「是」** 以便修改 DNS。

---

## 快速開始

### 1）選介面並套用 DNS

1. （可選) 按 **「掃描介面卡」** → 勾選要套用的介面 (可勾 **「包含進階/虛擬/撥接介面」** 顯示更多）。(預設會勾選WiFi/Ethernet)
2. 在左側選擇 DNS 來源：

   * 勾 **AdGuard** → 使用 AdGuard
   * 勾 **自動取得 DNS (DHCP)** → 還原為 DHCP
   * 兩者都不勾時，改用下方單選：**HiNet / Cloudflare / Google**
3. 按 **「套用設定」**。完成後可按 **「顯示目前 DNS」** 檢視結果。
4. 若應用程式未即時生效，可按 **「清除 DNS 快取」**（等同 `ipconfig /flushdns`）。

### 2）檢視紀錄與測試延遲

* 按 **「顯示紀錄：開/關」** 切換右側紀錄面板（自動展開視窗寬度）。
* 按 **「測試回應時間」**：對常見網域（YouTube/Steam/Netflix/Xbox/CDN 等）進行多次解析，輸出每個 DNS 的平均時間；**每個網域的最低值會加 `*`** 方便比對。

### 3）自動切換 DNS（依程式執行）

1. 按 **「自訂路徑」** 開啟路徑編輯器。
2. 在上方輸入：

   * **Directory**：完整 .exe 路徑（例如 `C:\\Games\\Steam\\steam.exe`）
   * **DNS**：目標 DNS 名稱（`AdGuard` / `HiNet` / `Cloudflare` / `Google` / `Dhcp`）
3. **應用**：寫入 `exePath.json` 但**不關閉**視窗。
   **保存**：寫入後**關閉**視窗。
   **關閉**：關閉（不執行存檔）。
4. 可在表格中**拖曳列**以調整優先順序（**越上面覆蓋優先級越高**）。
5. 回主畫面勾選 **「啟用/停用自動切換DNS」**。用數字框 **「掃描間隔秒數」** 調整輪詢頻率（預設 5 秒）。
6. 當清單中的任一程式正在執行 → 會自動切至對應的 DNS；沒找到符合項目 → **自動切回上次手動套用的 DNS**。

---

## `exePath.json` 格式

放在 `AutoDNS.exe` **同資料夾**；屬性為**完整 .exe 路徑**，值為**DNS 名稱**。

```json
{
  "C:\\Games\\Steam\\steam.exe": "Cloudflare",
  "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe": "AdGuard",
  "C:\\Windows\\System32\\mstsc.exe": "HiNet",
  "D:\\Portable\\qBittorrent\\qbittorrent.exe": "Dhcp"
}
```

> 編輯器會**保持屬性順序**，也能**拖曳重排**；主程式讀取時會依序檢查，**先命中者優先**。

---

## 介面與按鈕一覽

| 區塊/控制                           | 作用                                            |
| ------------------------------- | --------------------------------------------- |
| 啟用/停用自動切換DNS（核取）                | 開關自動切換功能；開啟後部分控制會鎖定避免衝突                       |
| 掃描間隔秒數（數字框）                     | 調整自動切換輪詢頻率（3–9999 秒）                          |
| 使用 AdGuard DNS / 自動取得 DNS (DHCP) | 兩者其一優先；都不勾時用下方單選（HiNet/Cloudflare/Google）     |
| HiNet / Cloudflare / Google | 在未勾 AdGuard/DHCP 時使用                          |
| 清除 DNS 快取                       | 執行 `ipconfig /flushdns`                       |
| 掃描介面卡                           | 重新列出可用介面；**完成選擇** 後返回主畫面                      |
| 顯示目前 DNS                        | 顯示勾選介面的 v4/v6 DNS 設定（同時寫入紀錄面板）                |
| 顯示紀錄：開/關                        | 切換右側紀錄面板顯示詳細資訊（最多保留 1000 行）                         |
| 套用設定                            | 依目前選擇對所有勾選介面的套用 DNS                            |
| 測試回應時間                          | 量測多網域 DNS 解析平均時間；每網域最低值加 `*`                  |
| 自訂路徑                            | 開啟 `exePath.json` 編輯器 |
| 清除輸出紀錄                          | 清空右側紀錄面板                                      |

---

## 限制

* **自動切換開啟時**，為避免衝突，手動 **「套用設定」** 及部分功能會禁用；請先關閉自動切換再手動操作。
* 主要支援 **乙太網路 / Wi‑Fi**；勾選「包含進階/虛擬/撥接介面」可納入 PPP/Unknown 等類型（不含 Loopback/Tunnel）。
* 若 PowerShell 指令失敗，程式會**自動改用 `netsh`** 後備套用。
* 視窗為**固定大小**（不能用滑鼠拖曳改尺寸），但在顯示紀錄/介面選擇時會自動調整寬度。
* 解析延遲測試會執行多次 DNS 查詢；**建議避免進行操作**，防止結果受影響。

---

## 疑難排解

* **無法套用/無權限**：請用**系統管理員身分**執行。
* **設定未立即生效**：斷線重連或按 **「清除 DNS 快取」**。
* **看不到介面卡**：按 **「掃描介面卡」**，或勾 **「包含進階/虛擬/撥接介面」**。
* **自動切換沒動作**：確認 `exePath.json` 路徑是否為**完整實際路徑**（不只檔名）、目標程式確實執行、掃描間隔是否過長。
* **PowerShell 輸出亂碼**：已強制 UTF-8；若仍異常，可試著調整系統語言/地區設定後再試。

---

## 待辦清單

* **1. 自訂 DNS 設定檔（規劃）**

  * 規劃：允許建立多組 DNS Profile（如：HiNet/Cloudflare/Google/AdGuard…），支援命名與多伺服器陣列。
 
* **2. 自訂 exePath + UI（完成）**

  * 現況：已能在 UI 中新增、編輯、刪除與排序 exe→DNS 的對應，並寫回 `exePath.json`。

* **3. 自訂網域的延遲測試（規劃）**

  * 規劃：使用者可維護測試清單（多網域、多次迭代），輸出 Avg/Min/Max，標註最快值。

* **4. 監控外傳 DNS 流量（規劃）**

  * 規劃：監控系統 DNS 查詢或 socket 目的地，依使用情境自動切換對應 DNS Profile。

* **5. 平行 DNS 查詢（規劃）**

  * 規劃：同時向多個 DNS 發問，採「最先回應」結果。

* **6. 系統匣與開機自啟（規劃）**

  * 規劃：加入 Tray Icon、右鍵快捷切換 Profile、開機自啟選項。


---

## Build

* Visual Studio 2022 / .NET 8（Windows Forms）。
* `exePath.json` already set `CopyToOutputDirectory=PreserveNewest` to make sure the the file exist along with AutoDNS.exe

---


