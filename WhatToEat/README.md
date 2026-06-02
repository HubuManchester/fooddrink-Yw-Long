# WhatToEat

**A cross-platform mobile app for food discovery, meal logging, and calorie tracking.**  
Built with .NET MAUI
---

## Overview

WhatToEat helps users decide what to eat, discover recipes, identify food using computer vision, and track their daily calorie intake. The app is built with .NET MAUI and runs on Android phones and Android tablets.

Key capabilities:
- Spin a food category wheel (or shake your phone) to get randomised recipe suggestions
- Take a photo of food — AI identifies it and estimates calories
- Log meals with automatic GPS location tagging
- Browse full recipes with ingredients and step-by-step instructions
- Review meal history grouped by day with calorie summaries
- Adjust font size, contrast, and dark mode via a dedicated Accessibility page

---

## Tech Stack

| Component | Technology |
|---|---|
| Framework | .NET MAUI (.NET 8) |
| Language | C# / XAML |
| Architecture | MVVM |
| Local Storage | SQLite (sqlite-net-pcl) |
| Recipe API | Spoonacular API |
| Food Recognition API | LogMeal AI API |
| Static Analysis | NDepend |

---

## Hardware Features

| Feature | API Used | Where |
|---|---|---|
| Camera | `MediaPicker.Default.CapturePhotoAsync` | PicturePage |
| Flash / Torch | `Flashlight.Default` | PicturePage |
| Computer Vision (AI) | LogMeal AI API + `Android.Graphics.BitmapFactory` | PicturePage |
| Geolocation + Geocoding | `Geolocation.Default` + `Geocoding.Default` | PicturePage → HistoryPage |
| Accelerometer / Shake | `Accelerometer.Default` at `SensorSpeed.Game` | HomePage |
| Haptic Feedback | `HapticFeedback.Default` | HomePage, PicturePage, HistoryPage |
| Text-to-Speech | `TextToSpeech.Default` + `CancellationToken` | HomePage, DiscoverPage, HistoryPage |

> Computer vision (LogMeal AI) satisfies the 86–100% band requirement for "advanced methods such as machine learning and computer vision alongside mobile hardware."

---

## Pages

- **Home** — Food category spin wheel, recipe card suggestions, pull-to-refresh, TTS read-aloud
- **Discover** — Full recipe detail (image, ingredients, numbered steps), keyword search
- **Picture** — Take photo → AI food recognition → log meal with location
- **History** — Date-grouped meal log, collapsible daily stats, delete and clear-all actions
- **Accessibility** — Font scale slider, high-contrast toggle, dark mode shortcut, screen reader notes

---

## Accessibility (WCAG 2.1)

| Feature | WCAG Criterion |
|---|---|
| Font size scaling (S / N / L / XL) via global `DynamicResource` | 1.4.4 Resize Text |
| High contrast mode via `Application.Current.UserAppTheme` | 1.4.3 / 1.4.6 Contrast |
| System dark mode deep-link | 1.4.3 |
| `SemanticProperties.Description` and `.Hint` on all controls | 4.1.2 Name, Role, Value |
| Touch target auto-scales with font (44pt → 56pt) | 2.5.5 Target Size |

---

## Environment Requirements

- .NET 8 SDK
- Android API 33 or higher (tested on API 33 and API 35)
- Visual Studio 2022 with MAUI workload installed
- Valid Spoonacular API key
- Valid LogMeal API user token

---

## Development Plan

### Planned features (implemented)
-  Food category spin wheel with shake gesture trigger
-  Spoonacular recipe search and detail view
-  Camera + LogMeal AI food recognition
-  GPS location tagging on meal logs
-  SQLite meal history with date grouping
-  Accessibility page (font scale, high contrast, dark mode, TTS, screen reader)
-  Pull-to-refresh on recipe suggestions
-  TTS with live pause/resume on all pages
-  NDepend static analysis (Tech Debt Rating: A)
-  Deployment to physical Android phone (Xiaomi, API 33) and Android tablet emulator (7.6in, API 35)

### Key fixes during development
- Replaced `ScrollView` wrapping `CollectionView` with `BindableLayout` to fix height measurement issue on MAUI
- Added image compression (`BitmapFactory`, JPEG quality stepping from 85) to stay under LogMeal's 1 MB upload limit
- Moved accelerometer callback to UI thread via `MainThread.BeginInvokeOnMainThread`
- Added `CancellationToken` to TTS to enable immediate stop instead of waiting for sentence end

---

## Author

**Student Number:** Yunwei Long  
**Student ID:** 21906298  
