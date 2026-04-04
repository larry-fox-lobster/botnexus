# Simple test to trigger a tool call and capture Anthropic API logging
Write-Host "=== Testing Multi-Turn Tool Call Flow ===" -ForegroundColor Cyan
Write-Host "This will capture the Anthropic API request messages..." -ForegroundColor Yellow
Write-Host ""

cd Q:\repos\botnexus

# Start API server
Write-Host "Starting API server..." -ForegroundColor Cyan
$apiJob = Start-Job -ScriptBlock {
    cd Q:\repos\botnexus
    dotnet run --project src\BotNexus.Api\BotNexus.Api.csproj
}

Start-Sleep -Seconds 10
Write-Host "✅ API server started (Job ID: $($apiJob.Id))" -ForegroundColor Green

try {
    # Send a message that will trigger a tool call
    Write-Host "`nSending tool call request..." -ForegroundColor Cyan
    
    $body = @{
        content = "Please list the files in the current directory using the filesystem tool"
        sessionKey = "test-msg-flow-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        channel = "console"
        chatId = "test"
    } | ConvertTo-Json

    $response = Invoke-RestMethod `
        -Uri "http://localhost:5000/api/chat" `
        -Method Post `
        -Body $body `
        -ContentType "application/json" `
        -TimeoutSec 60

    Write-Host "`n✅ Response received:" -ForegroundColor Green
    Write-Host $response.content -ForegroundColor White

    Write-Host "`n📋 Check the API logs above for:" -ForegroundColor Yellow
    Write-Host "  - 'Anthropic Messages API Request' (shows the full JSON payload)" -ForegroundColor Yellow
    Write-Host "  - Look for tool_result messages in the payload" -ForegroundColor Yellow

} catch {
    Write-Host "`n❌ Error: $_" -ForegroundColor Red
} finally {
    Write-Host "`nStopping API server..." -ForegroundColor Cyan
    Stop-Job -Job $apiJob -ErrorAction SilentlyContinue
    Remove-Job -Job $apiJob -Force -ErrorAction SilentlyContinue
    Write-Host "✅ API server stopped" -ForegroundColor Green
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
