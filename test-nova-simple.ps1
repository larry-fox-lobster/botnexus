# Simple Nova test with single tool call
$webSocket = New-Object System.Net.WebSockets.ClientWebSocket
$uri = [System.Uri]::new("ws://localhost:18790/ws?agent=nova")
$cts = New-Object System.Threading.CancellationTokenSource

try {
    Write-Host "Connecting..." -ForegroundColor Cyan
    $webSocket.ConnectAsync($uri, $cts.Token).GetAwaiter().GetResult()
    Write-Host "Connected!" -ForegroundColor Green

    $message = @{
        type = "user_message"
        content = "Use the time tool to get the current time"
    } | ConvertTo-Json

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($message)
    $segment = [System.ArraySegment[byte]]::new($bytes)
    
    Write-Host "Sending: $($message)" -ForegroundColor Yellow
    $webSocket.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).GetAwaiter().GetResult()

    $buffer = New-Object byte[] 8192
    $segment = [System.ArraySegment[byte]]::new($buffer)
    
    for ($i = 0; $i -lt 10; $i++) {
        $result = $webSocket.ReceiveAsync($segment, $cts.Token).GetAwaiter().GetResult()
        if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) { break }
        
        $responseText = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count)
        $response = $responseText | ConvertFrom-Json
        
        Write-Host "[$i] $($response.type): $($response.content)" -ForegroundColor White
    }

} finally {
    if ($webSocket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $webSocket.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", $cts.Token).GetAwaiter().GetResult()
    }
    $webSocket.Dispose()
    $cts.Dispose()
}
