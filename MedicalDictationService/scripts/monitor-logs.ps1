Write-Host "🔍 Medical Dictation Service - Debug Log Monitor" -ForegroundColor Green
Write-Host "Click START button in the browser to see debug logs..." -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop monitoring" -ForegroundColor Yellow
Write-Host ""

# Run the application with verbose logging and filter for our debug messages
dotnet run --verbosity normal 2>&1 | Where-Object { 
    $_ -match "🚀|📋|🔄|✅|❌|💥|🎤|📝|💾|🔧|🎵|🔊|🏗️|🎯|▶️|🟢|📡|🎉|⚠️" -or
    $_ -match "StartTranscription|Azure|Speech|Hub|Session|Error|Exception" 
} | ForEach-Object {
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Host "[$timestamp] $_" -ForegroundColor Cyan
} 