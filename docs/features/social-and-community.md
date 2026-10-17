# Pelacakan Sosial, Misi & Balai Kota

Fitur pelacakan interaksi sosial dengan penduduk desa, kuota hadiah mingguan, misi pribadi, dan progres pemugaran Balai Kota (Community Center / Joja Development).

## Fitur Utama

### 1. Pelacakan Hubungan Sosial (Social Relationships)
- **Tingkat Hati & Poin Pertemanan**: Menampilkan jumlah hati (0-10 untuk penduduk biasa, hingga 14 untuk pasangan/suami-istri) beserta akumulasi poin persahabatan aktual (250 poin per hati).
- **Jatah Hadiah Mingguan (Weekly Gift Quota)**: Memantau sisa kuota pemberian hadiah per minggu (standar 2 hadiah per minggu per NPC) dan status pemberian hadiah hari ini.
- **Deteksi Ulang Tahun (Birthday Alert)**: Menandai penduduk yang berulang tahun hari ini secara otomatis dengan pengingat bonus poin hadiah 8x lipat.
- **Status Sapaan Harian (Daily Conversation)**: Menampilkan apakah petani sudah menyapa NPC hari ini untuk mendapatkan bonus +20 poin persahabatan harian.
- **Penyaringan & Pengurutan**: Filter penduduk yang bisa dinikahi (*bachelors/bachelorettes*), yang belum mencapai hati maksimal, atau yang belum disapa hari ini.

### 2. Progres Balai Kota (Community Center) & Proyek Joja
- **Pemugaran Ruangan Demi Ruangan**: Memantau status setiap ruangan (Pantry, Crafts Room, Fish Tank, Boiler Room, Vault, Bulletin Board, Abandoned JojaMart).
- **Daftar Bundel Lengkap**: Menampilkan item yang dibutuhkan (*ingredients*), jumlah, kualitas minimal (Perak, Emas, Iridium), dan status pemenuhannya.
- **Rute Joja Corporation**: Jika memilih rute keanggotaan Joja, sistem secara otomatis beralih menampilkan daftar proyek pembangunan komunitas Joja beserta biaya emas dan status pembeliannya.

### 3. Jurnal Misi (Personal Quests) & Pesanan Khusus (Special Orders)
- **Buku Catatan Misi Harian & Cerita**: Menampilkan misi Help Wanted aktif dari papan buletin Pierre, deskripsi tujuan, hadiah uang, dan sisa waktu pengerjaan.
- **Pesanan Khusus Komunitas & Ruang Qi**: Memantau pesanan jangka panjang dari papan pesanan walikota Lewis dan Mister Qi dengan progress bar objektif interaktif.

## Integrasi MediaWiki Stardew Valley Wiki

Sistem Web Admin secara otomatis memuat aset grafis asli beresolusi tinggi langsung dari Stardew Valley Wiki menggunakan algoritma hashing MediaWiki MD5:
- **Portret NPC**: Wajah asli seluruh penduduk dan karakter spesial.
- **Ikon Bundel & Ruangan**: Banner visual setiap bundel persembahan Junimo dan ruangan Balai Kota.
- **Ikon Item**: Grafis item tanaman, ikan, hasil ternak, dan bahan tambang dengan fallback SVG vektor jika koneksi offline atau terkena filter jaringan.

## REST API Endpoints

- `GET /api/farmer/social?farmerId={id}`: Mengambil data persahabatan, kuota hadiah, dan ulang tahun penduduk.
- `GET /api/farmer/quests?farmerId={id}`: Mengambil daftar misi aktif dan pesanan khusus pemain.
- `GET /api/farmer/community-center`: Mengambil progres ruangan, bundel, dan proyek Joja dunia saat ini.
