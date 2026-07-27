# 📊 TradeVault — AI-Powered Trading Journal & Analytics

**TradeVault** is a modern, high-performance trading journal and analytics web application built with **ASP.NET Core 10 Blazor Server** and powered by **Google Gemini AI Vision**. It automatically reads trading chart screenshots to extract entry prices, stop losses, take profit levels, patterns, and trade outcomes.

---

## ✨ Features

- 🤖 **Google Gemini Vision AI Integration**:
  - **Before-Trade Chart OCR & Extraction**: Auto-detects Entry, Stop Loss, Take Profit, Market Pair, Timeframe, Direction (BUY/SELL), and Strategy Pattern from chart screenshots.
  - **After-Trade Comparison Analysis**: Compares *Before* and *After* chart screenshots to auto-evaluate trade outcomes (WIN, LOSS, BREAKEVEN) and actual R:R achieved.
  - **Auto Summarization**: Generates AI insights and setup notes based on technical price action.
- 📈 **Comprehensive Trade Journal**:
  - Log trades across Forex, Crypto, Stocks, Indices, Commodities, and Options.
  - Track live Risk/Reward (R:R) ratios, risk percentages, lot sizes, and P&L.
  - Record pre-trade psychology, confidence scores (1-10), rule mistakes, and key lessons.
- 📊 **Dashboard & Analytics**:
  - Live performance statistics: Win Rate, Total R:R, Total P&L, Average Winner/Loser.
  - Interactive charts and equity curves.
- 🎯 **Goals & Achievements**:
  - Gamified achievement badges (Streak Master, 10 RR Month, Disciplined Trader).
  - Custom monthly and weekly target goals with progress tracking.
- 🐳 **Docker & Cloud Ready**:
  - Pre-configured `Dockerfile` for instant 1-click deployment on cloud platforms like Koyeb, Render, Railway, Back4app, or Azure.

---

## 🛠️ Technology Stack

- **Framework**: .NET 10 (ASP.NET Core Blazor Server Interactive)
- **Database**: Entity Framework Core 10 with SQLite
- **AI Engine**: Google Gemini API (`gemini-2.0-flash` / `gemini-1.5-flash`)
- **Authentication**: ASP.NET Core Identity
- **UI/Styling**: Custom Modern Glassmorphic CSS Design System

---

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) installed on your machine.
- A free [Google Gemini API Key](https://aistudio.google.com/).

### Local Installation & Setup

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/Muhammadatif153700/TradeVault.git
   cd TradeVault
   ```

2. **Configure Gemini API Key** (Optional):
   Open `appsettings.json` and set your key:
   ```json
   "GeminiApiKey": "YOUR_GEMINI_API_KEY_HERE"
   ```
   *(Note: You can also input your Gemini API Key directly inside the app UI when logging a trade!)*

3. **Run the Application**:
   ```bash
   dotnet run
   ```

4. Open your browser and navigate to:
   ```
   http://localhost:5173
   ```

---

## 🐳 Docker & Cloud Deployment

This repository includes a production-ready `Dockerfile`.

### Build & Run locally with Docker:
```bash
docker build -t tradevault .
docker run -p 8080:8080 tradevault
```

### Deploy to Free Cloud Platforms:
1. Connect this GitHub repository to **Koyeb**, **Back4app**, **Render**, or **Railway**.
2. Select **Docker** as the deployment builder.
3. Your app will automatically build and deploy with a free public URL!

---

## 📝 License

This project is open-source and available under the [MIT License](LICENSE).
