# Script tự động cập nhật code lên Oracle Cloud VPS
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  BẮT ĐẦU CẬP NHẬT CODE LÊN ORACLE CLOUD  " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$serverIp = "168.110.56.11"
$keyFile = "f:\tools\stock-trading-system\oracle.key"

# 1. Biên dịch code
Write-Host "[1/3] Đang biên dịch code Release cho Linux x64..." -ForegroundColor Yellow
dotnet publish "src\TradingSystem.Collector\TradingSystem.Collector.csproj" -c Release -r linux-x64 --self-contained false -o "publish-linux"

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Biên dịch thất bại! Đã hủy cập nhật." -ForegroundColor Red
    exit 1
}

# 2. Tạm dừng service trước khi ghi đè file
Write-Host "[2/4] Đang tạm dừng dịch vụ trên VPS để cập nhật file..." -ForegroundColor Yellow
ssh -i $keyFile -o StrictHostKeyChecking=no ubuntu@${serverIp} "sudo systemctl stop trading-collector.service"

# 3. Đẩy file lên VPS
Write-Host "[3/4] Đang tải các file cập nhật lên máy chủ..." -ForegroundColor Yellow
scp -i $keyFile -o StrictHostKeyChecking=no -r publish-linux/* ubuntu@${serverIp}:/home/ubuntu/trading-system/

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Lỗi khi tải file qua SCP!" -ForegroundColor Red
    ssh -i $keyFile -o StrictHostKeyChecking=no ubuntu@${serverIp} "sudo systemctl start trading-collector.service"
    exit 1
}

# 4. Khởi động lại service trên VPS để nhận code mới
Write-Host "[4/4] Đang khởi động lại dịch vụ bot trên VPS..." -ForegroundColor Yellow
ssh -i $keyFile -o StrictHostKeyChecking=no ubuntu@${serverIp} "chmod +x /home/ubuntu/trading-system/TradingSystem.Collector && sudo systemctl start trading-collector.service && sudo systemctl status trading-collector.service --no-pager"

Write-Host "==========================================" -ForegroundColor Green
Write-Host "  ✅ CẬP NHẬT HOÀN TẤT VÀ BOT ĐANG CHẠY!   " -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green

