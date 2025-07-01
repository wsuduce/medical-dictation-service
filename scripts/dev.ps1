#!/usr/bin/env pwsh
# Development startup script for Medical Dictation Service
# Usage: .\scripts\dev.ps1

Write-Host "🏥 Medical Dictation Service - Development Startup" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# Check current directory
if (!(Test-Path "MedicalDictationService.csproj")) {
    Write-Host "❌ Run this script from the MedicalDictationService directory" -ForegroundColor Red
    exit 1
}

# 1. Check for Azure Speech credentials
Write-Host "🔍 Checking Azure Speech Services configuration..." -ForegroundColor Yellow

$speechKey = dotnet user-secrets get "AzureSpeech:SubscriptionKey" 2>$null
$speechRegion = dotnet user-secrets get "AzureSpeech:Region" 2>$null

if ([string]::IsNullOrEmpty($speechKey) -or [string]::IsNullOrEmpty($speechRegion)) {
    Write-Host "⚠️  No Azure Speech Services credentials found" -ForegroundColor Yellow
    Write-Host "   Running in DEMO MODE (local medical terminology only)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To enable Azure Speech Services:" -ForegroundColor Gray
    Write-Host "   dotnet user-secrets set `"AzureSpeech:SubscriptionKey`" `"<YOUR_KEY>`"" -ForegroundColor Gray
    Write-Host "   dotnet user-secrets set `"AzureSpeech:Region`" `"<YOUR_REGION>`"" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host "✅ Azure Speech Services configured (Region: $speechRegion)" -ForegroundColor Green
}

# 2. Run Entity Framework migrations
Write-Host "🗄️  Checking database..." -ForegroundColor Yellow

try {
    dotnet ef database update --no-build 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Database updated successfully" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Database update skipped (no migrations or already up to date)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠️  Entity Framework not available or no migrations" -ForegroundColor Yellow
}

# 3. Build the application
Write-Host "🔨 Building application..." -ForegroundColor Yellow
dotnet build --no-restore -q

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build successful" -ForegroundColor Green

# 4. Launch application and browser
Write-Host "🚀 Starting Medical Dictation Service..." -ForegroundColor Yellow
Write-Host ""
Write-Host "📍 Application URLs:" -ForegroundColor Cyan
Write-Host "   Main App: http://localhost:5080" -ForegroundColor White
Write-Host "   Transcription: http://localhost:5080/transcription" -ForegroundColor White
Write-Host ""
Write-Host "🔐 Demo Credentials:" -ForegroundColor Cyan
Write-Host "   Username: admin" -ForegroundColor White
Write-Host "   Password: Admin123!" -ForegroundColor White
Write-Host ""
Write-Host "⏹️  Press Ctrl+C to stop the application" -ForegroundColor Gray
Write-Host ""

# Auto-open browser after a short delay
Start-Job -ScriptBlock {
    Start-Sleep 3
    Start-Process "http://localhost:5080"
} | Out-Null

# Start the application
dotnet run --no-build 