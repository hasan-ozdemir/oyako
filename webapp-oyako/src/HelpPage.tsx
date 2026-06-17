// Codex developer note: Explains the exhaustive Turkish help page that teaches users how to operate Oyako.
import {
  Accessibility,
  BellRing,
  BookOpen,
  Bot,
  BrainCircuit,
  CheckCircle2,
  Database,
  HelpCircle,
  Keyboard,
  MessageSquareText,
  Settings,
  ShieldCheck,
  Smartphone,
  Sparkles,
  UserCircle,
} from 'lucide-react'

// Defines the properties required by the modal-style Help page.
interface HelpPageProps {
  // Closes the Help page and returns the user to the main Oyako workspace.
  onBack: () => void
}

// Describes a single Help section rendered as an accessible card.
interface HelpTopic {
  // Stores the icon component that visually identifies the Help topic.
  icon: typeof HelpCircle
  // Stores the Turkish title shown to end users.
  title: string
  // Stores the Turkish explanation shown to end users.
  text: string
}

// Lists every current user-facing capability that the Help page must explain.
const helpTopics: HelpTopic[] = [
  {
    icon: Bot,
    title: 'Oyako nedir?',
    text: 'Oyako, Oyak Dijital web kaynaklarından beslenen tek sayfalık bir soru-cevap platformudur. Oyak Dijital hakkında merak ettiğiniz konuları doğal Türkçe ile sorabilir, yanıtları canlı olarak takip edebilirsiniz.',
  },
  {
    icon: MessageSquareText,
    title: 'Soru-Cevap Panosu',
    text: 'Sorduğunuz sorular ve Oyako yanıtları ekranda “Siz” ve “Oyako” başlıklarıyla sırayla görünür. Görsel sohbet geçmişi korunur; ancak backend tarafında her soru tek seferlik ve geçmiş zinciri olmadan işlenir.',
  },
  {
    icon: BrainCircuit,
    title: 'Tek seferlik yapay zekâ akışı',
    text: 'Her soruda sistem talimatları ve güncel bilgi kaynakları yeniden kullanılır; hedef yapay zekâ modeline yalnızca system instruction ve tek kullanıcı sorusu gönderilir. Bu yaklaşım cevapların kontrol edilebilir kalmasına yardım eder.',
  },
  {
    icon: Database,
    title: 'Bilgi Bankası',
    text: 'Bilgi Bankası penceresi Oyako’nun hangi Oyak Dijital sayfalarından beslendiğini gösterir. Kaynak başlıklarını, URL bağlantılarını, son tarama zamanını ve temizlenmiş önizleme metinlerini buradan inceleyebilirsiniz.',
  },
  {
    icon: Sparkles,
    title: 'Hazır ve önerilen sorular',
    text: 'Hazır sorular backend tarafından üretilir ve tek tıkla gönderilebilir. Oyako yanıtlarının sonunda yalnızca son cevaba ait önerilen sorular gösterilir; bu önerilere tıklayınca yeni soru otomatik gönderilir.',
  },
  {
    icon: Settings,
    title: 'AI tedarikçisi ve model ayarları',
    text: 'Ayarlar penceresinden Azure, Ollama Local veya Ollama Cloud tedarikçisi ve desteklenen model seçilebilir. Değişiklikler kaydedilirken erişilebilir ilerleme bildirimi gösterilir ve işlem tamamlandığında kullanıcı bilgilendirilir.',
  },
  {
    icon: BellRing,
    title: 'Canlı durum çubuğu',
    text: 'Ekranın altındaki durum çubuğu uygulama yükleniyor, hazır, bilgiler alınıyor, bilgiler kaydediliyor, cevap veriliyor veya işlem kontrol edilmeli gibi süreçleri simge ve metinle gösterir. Bu alan boş kalmayacak şekilde tasarlanmıştır.',
  },
  {
    icon: ShieldCheck,
    title: 'Kaynak sınırlı cevap üretimi',
    text: 'Oyako, Oyak Dijital kaynaklarından gelen metinlerin dışında bilgi vermemek üzere tasarlanmıştır. Cevabın kaynaklarda bulunmadığı durumlarda bunu açıkça belirtir ve sorulabilecek geçerli örnek sorular önerir.',
  },
  {
    icon: Keyboard,
    title: 'Klavye kullanımı',
    text: 'Soru kutusundayken Enter soruyu gönderir. Shift+Enter veya Ctrl+Enter yeni satır açar. Escape tuşu açık pencereleri ve menüleri kapatır. Görünür odak halkaları klavye ile gezinmeyi kolaylaştırır.',
  },
  {
    icon: Accessibility,
    title: 'Erişilebilirlik',
    text: 'Başlıklar, canlı bölgeler, menüler, dialog pencereleri, ilerleme durumları ve buton etiketleri ekran okuyucu teknolojileriyle uyumlu olacak şekilde yapılandırılmıştır. Kritik işlemler a11y alert ile duyurulur.',
  },
  {
    icon: Smartphone,
    title: 'Responsive ve PWA deneyimi',
    text: 'Oyako masaüstü, tablet ve mobil ekranlarda kullanılabilir. Tek sayfalık modern uygulama yapısı sayesinde destekleyen tarayıcılarda uygulama benzeri tam ekran deneyim sağlar.',
  },
  {
    icon: UserCircle,
    title: 'Kullanıcı ve bağlantılar menüleri',
    text: 'Header’daki kullanıcı avatarı giriş/kayıt seçeneklerini gösterir. Durum çubuğundaki “Daha Fazla...” menüsü Oyak Dijital bağlantısını, geri bildirim e-postasını ve bu yardım ekranını açar.',
  },
  {
    icon: CheckCircle2,
    title: 'Yanıt doğruluğu',
    text: 'Oyako kaynaklara bağlı cevap üretse de, kritik kararlar öncesinde yanıtların doğruluğunu kontrol edin. Durum çubuğundaki uyarı bu kalite ve doğrulama yaklaşımını hatırlatır.',
  },
]

// Renders the complete Help dialog with current Oyako capabilities in Turkish.
function HelpPage({ onBack }: HelpPageProps) {
  // Returns the full-screen Help document as an accessible modal region.
  return (
    <div className="help-page" role="dialog" aria-modal="true" aria-labelledby="help-title">
      {/* Provides the modal navigation and document heading for assistive technologies. */}
      <div className="help-page-top">
        {/* Lets the user return from Help to the main Oyako workspace. */}
        <button type="button" className="back-button" onClick={onBack}>
          <HelpCircle size={18} aria-hidden="true" />
          <span>Geri</span>
        </button>
        {/* Introduces the Help page purpose with Turkish user-facing copy. */}
        <div>
          <p className="eyebrow">Yardım</p>
          <h2 id="help-title">Oyako nasıl kullanılır?</h2>
        </div>
      </div>

      {/* Summarizes the complete product experience before the detailed topic cards. */}
      <section className="help-hero" aria-label="Oyako yardım özeti">
        <BookOpen size={34} aria-hidden="true" />
        <div>
          <h3>Oyak Dijital için güncel, kaynaklı ve erişilebilir soru-cevap deneyimi</h3>
          <p>
            Bu yardım ekranı Oyako’nun soru sorma, bilgi kaynaklarını inceleme,
            hazır soruları kullanma, ayarları değiştirme, yanıtları doğrulama ve erişilebilir şekilde
            gezinme özelliklerini uçtan uca açıklar.
          </p>
        </div>
      </section>

      {/* Lists every current feature as a scan-friendly Help card. */}
      <div className="help-grid" aria-label="Oyako özellikleri">
        {helpTopics.map((topic) => {
          // Keeps icon rendering data-driven so Help content remains easy to extend.
          const Icon = topic.icon

          // Renders one Help topic with a visual icon, Turkish heading, and Turkish explanation.
          return (
            <article className="help-card" key={topic.title}>
              {/* Shows a decorative icon that supports quick visual recognition. */}
              <span className="help-card-icon" aria-hidden="true">
                <Icon size={20} />
              </span>
              {/* Names the feature being explained in this card. */}
              <h3>{topic.title}</h3>
              {/* Explains the feature in plain Turkish for end users. */}
              <p>{topic.text}</p>
            </article>
          )
        })}
      </div>
    </div>
  )
}

// Exposes the Help page so App.tsx can mount it from the Help menu.
export default HelpPage
