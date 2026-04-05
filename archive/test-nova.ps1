# Test Nova agent multi-turn tool calling
$webSocket = New-Object System.Net.WebSockets.ClientWebSocket
$uri = [System.Uri]::new("ws://localhost:18790/ws?agent=nova")
$cts = New-Object System.Threading.CancellationTokenSource

try {
    Write-Host "Connecting to WebSocket..." -ForegroundColor Cyan
    $webSocket.ConnectAsync($uri, $cts.Token).GetAwaiter().GetResult()
    Write-Host "Connected!" -ForegroundColor Green

    # Send test message
    $message = @{
        type = "user_message"
        content = "List the files in the current directory"
    } | ConvertTo-Json

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($message)
    $segment = [System.ArraySegment[byte]]::new($bytes)
    
    Write-Host "Sending message: $($message)" -ForegroundColor Yellow
    $webSocket.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).GetAwaiter().GetResult()

    # Receive responses
    $buffer = New-Object byte[] 8192
    $segment = [System.ArraySegment[byte]]::new($buffer)
    
    Write-Host "`nWaiting for responses..." -ForegroundColor Cyan
    for ($i = 0; $i -lt 20; $i++) {
        $result = $webSocket.ReceiveAsync($segment, $cts.Token).GetAwaiter().GetResult()
        if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
            Write-Host "Connection closed by server" -ForegroundColor Red
            break
        }
        
        $responseText = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count)
        $response = $responseText | ConvertFrom-Json
        
        Write-Host "`n=== Response $($i+1) ===" -ForegroundColor Magenta
        Write-Host "Type: $($response.type)" -ForegroundColor White
        if ($response.content) {
            Write-Host "Content: $($response.content)" -ForegroundColor White
        }
        if ($response.delta) {
            Write-Host "Delta: $($response.delta)" -ForegroundColor Gray
        }
        
        if ($response.type -eq "final_response" -or $result.EndOfMessage -and $i -gt 5) {
            break
        }
    }

} finally {
    Write-Host "`nClosing connection..." -ForegroundColor Cyan
    if ($webSocket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $webSocket.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Test complete", $cts.Token).GetAwaiter().GetResult()
    }
    $webSocket.Dispose()
    $cts.Dispose()
    Write-Host "Done!" -ForegroundColor Green
}
