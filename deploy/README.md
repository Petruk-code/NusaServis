# Yayın: Oracle Cloud + Caddy

Uygulama Oracle Cloud Always Free ARM sunucusunda (Ubuntu, Frankfurt) systemd servisi olarak çalışır; önünde
Caddy (HTTPS) vardır. Veritabanı Neon (Frankfurt), e-posta Resend (İrlanda).

## İlk kurulum (sunucuda bir kez)

```sh
sudo mkdir -p /opt/NusaServis /etc/NusaServis
sudo install -m 600 /dev/null /etc/NusaServis/NusaServis.env   # sonra sudoedit ile doldurulur
sudo cp NusaServis.service /etc/systemd/system/
sudo systemctl daemon-reload && sudo systemctl enable NusaServis
# Caddyfile'daki site bloğunu /etc/caddy/Caddyfile'a ekleyip: sudo systemctl reload caddy
```

`/etc/NusaServis/NusaServis.env` (değerler `.env.example`'daki adlarla):

```
DATABASE_URL=postgresql://...            # Neon, direct bağlantı
RESEND_API_KEY=re_...
EMAIL_FROM=NusaServis <NusaServis@miracdeprem.com>
PUBLIC_ORIGIN=https://NusaServis.miracdeprem.com
AllowedHosts=NusaServis.miracdeprem.com
VAPID_PUBLIC_KEY=...
VAPID_PRIVATE_KEY=...
VAPID_SUBJECT=mailto:mrcdprmm@hotmail.com
```

## Yeni sürüm

**Önce SDK'yı güncelle.** Paket .NET çalışma ortamını içinde taşır; sürümü bilgisayardaki SDK belirler. SDK eskiyse
sunucu güvenlik yaması almamış bir çalışma ortamıyla açılır (2026-11-03 taramasında 10.0.9 bulundu). Açılışta
`journalctl -u NusaServis | grep "NusaServis açılıyor"` sürümü gösterir.

```powershell
winget install Microsoft.DotNet.SDK.10
dotnet --list-runtimes
```

Bilgisayarda (repo kökünde) Linux ARM için tek klasörlük, .NET kurulumu gerektirmeyen bir yayın üretilir. ReadyToRun:
kod önceden derlenir, küçük işlemcide ilk açılışlar hızlanır.

```sh
dotnet publish src/Tamirhane.Web -c Release -r linux-arm64 --self-contained -p:PublishReadyToRun=true -o dist/linux-arm64
tar -czf dist/NusaServis.tar.gz -C dist/linux-arm64 .
scp dist/NusaServis.tar.gz ubuntu@SUNUCU:/tmp/
ssh ubuntu@SUNUCU 'sudo rm -rf /opt/NusaServis/* && sudo tar --no-same-owner -xzf /tmp/NusaServis.tar.gz -C /opt/NusaServis && sudo chown -R root:root /opt/NusaServis && sudo chmod 755 /opt/NusaServis/Tamirhane.Web && sudo systemctl restart NusaServis'
```

Windows'ta paketlenen arşiv dosyaların "çalıştırılabilir" iznini ve sahibini taşımaz: `chmod` olmadan servis
`status=203/EXEC` ile açılmaz (2026-11-03'te bir kez oldu), `--no-same-owner` ve `chown` olmadan dosyalar
Windows kullanıcı numarasına ait kalır.

Migration'lar uygulama açılırken uygulanır. Kontrol: `curl -s https://NusaServis.miracdeprem.com/health` → `Healthy`.
