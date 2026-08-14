using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace GumrukTarifeOyunu;

public partial class MainPage : ContentPage, INotifyPropertyChanged
{
    private Color _optionBgColor = Colors.Black;
    public Color OptionBgColor { get => _optionBgColor; set { _optionBgColor = value; OnPropertyChanged(nameof(OptionBgColor)); } }

    private Color _optionTextColor = Colors.White;
    public Color OptionTextColor { get => _optionTextColor; set { _optionTextColor = value; OnPropertyChanged(nameof(OptionTextColor)); } }

    private readonly List<CustomsTariff> allTariffs;
    private readonly List<CustomsTariff> wrongTariffs = [];
    private readonly List<CustomsTariff> notebookList = [];
    private readonly ObservableCollection<CustomsTariff> displayedTariffs = [];

    private int correctCount;
    private int currentQuestionIndex;
    private List<CustomsTariff> sessionTariffs = [];
    private List<CustomsTariff> currentOptions = [];
    private CustomsTariff currentQuestion = new("", "", "");

    private bool isReviewMode;
    private bool isCodeToDescMode;
    private bool isQuestionAnswered;

    public MainPage()
    {
        InitializeComponent();
        allTariffs = GetMyTariffList();
        BindingContext = this;

        if (ColAllWords != null) ColAllWords.ItemsSource = displayedTariffs;
        if (PckTheme != null) PckTheme.SelectedIndex = 0;

        StartNewGame();
    }

    private void OnThemeChanged(object sender, EventArgs e)
    {
        if (PckTheme == null || sender == null || e == null) return;

        switch (PckTheme.SelectedIndex)
        {
            case 0: OptionBgColor = Colors.Black; OptionTextColor = Colors.White; break;
            case 1: OptionBgColor = Colors.Yellow; OptionTextColor = Colors.Black; break;
            case 2: OptionBgColor = Color.FromArgb("#1F618D"); OptionTextColor = Colors.White; break;
            case 3: OptionBgColor = Color.FromArgb("#1D8348"); OptionTextColor = Colors.White; break;
        }
    }

    private void OnFilterChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (sender == null || e == null) return;
        StartNewGame();
    }

    private void StartNewGame()
    {
        correctCount = 0;
        currentQuestionIndex = 0;

        var sourceList = (isReviewMode && wrongTariffs.Count > 0) ? wrongTariffs : allTariffs;

        List<string> selectedChapters = [];
        if (ChkSection1 is { IsChecked: true }) selectedChapters.Add("Bölüm 1");
        if (ChkSection2 is { IsChecked: true }) selectedChapters.Add("Bölüm 2");
        if (ChkSection3 is { IsChecked: true }) selectedChapters.Add("Bölüm 3");
        if (ChkSection4 is { IsChecked: true }) selectedChapters.Add("Bölüm 4");

        if (selectedChapters.Count == 0)
        {
            selectedChapters = ["Bölüm 1", "Bölüm 2", "Bölüm 3", "Bölüm 4"];
        }

        sessionTariffs = [.. sourceList
            .Where(t => selectedChapters.Contains(t.Chapter))
            .OrderBy(_ => Guid.NewGuid())
            .Take(50)];

        LoadNextQuestion(null, null);
    }

    public void LoadNextQuestion(object? sender, EventArgs? e)
    {
        _ = sender;
        _ = e;

        if (sessionTariffs.Count == 0)
        {
            LblEnglishWord.Text = "Kayıt Yok";
            LblStatus.Text = "Seçilen bölümde tarife pozisyonu bulunamadı.";
            return;
        }

        if (currentQuestionIndex >= sessionTariffs.Count) { StartNewGame(); return; }

        currentQuestion = sessionTariffs[currentQuestionIndex];
        isQuestionAnswered = false;

        var sameChapterPool = allTariffs.Where(t => t.Chapter == currentQuestion.Chapter && t.Code != currentQuestion.Code).ToList();
        if (sameChapterPool.Count < 3) sameChapterPool = allTariffs.Where(t => t.Code != currentQuestion.Code).ToList();

        var wrongChoices = sameChapterPool.OrderBy(_ => Guid.NewGuid()).Take(3).ToList();
        currentOptions = [.. wrongChoices, currentQuestion];
        currentOptions = [.. currentOptions.OrderBy(_ => Guid.NewGuid())];

        ResetOptionViews();
        UpdateUIContent();
        currentQuestionIndex++;
    }

    private void UpdateUIContent()
    {
        if (currentQuestion == null || sessionTariffs.Count == 0) return;

        LblEnglishWord.Text = isCodeToDescMode ? currentQuestion.Code : currentQuestion.Description;
        LblStatus.Text = $"Soru: {currentQuestionIndex + (BtnNext.IsVisible ? 0 : 1)}/{sessionTariffs.Count} | Doğru: {correctCount}";

        Label[] optionLabels = [LblOpt1, LblOpt2, LblOpt3, LblOpt4];
        for (int i = 0; i < 4; i++)
        {
            if (i < currentOptions.Count)
                optionLabels[i].Text = isCodeToDescMode ? currentOptions[i].Description : currentOptions[i].Code;
        }

        if (!isQuestionAnswered) LblOverallResult.Text = "";
    }

    private void OnOptionTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Border border || e == null || isQuestionAnswered || sessionTariffs.Count == 0) return;

        var tapGesture = border.GestureRecognizers.FirstOrDefault() as TapGestureRecognizer;
        string? parameter = tapGesture?.CommandParameter?.ToString();

        if (int.TryParse(parameter, out int index))
        {
            isQuestionAnswered = true;
            BtnNext.IsVisible = true;

            Label[] rbLabels = [Rb1, Rb2, Rb3, Rb4];
            rbLabels[index].Text = "◉";

            if (currentOptions[index] == currentQuestion)
            {
                correctCount++;
                LblOverallResult.Text = "DOĞRU! ✔";
                LblOverallResult.TextColor = Colors.LightGreen;
                ShowFeedback(index);
                wrongTariffs.Remove(currentQuestion);
            }
            else
            {
                LblOverallResult.Text = $"YANLIŞ! ✘ (Doğru: {currentQuestion.Code} - {currentQuestion.Description})";
                LblOverallResult.TextColor = Colors.Salmon;
                ShowFeedback(currentOptions.IndexOf(currentQuestion));
                if (!wrongTariffs.Contains(currentQuestion)) wrongTariffs.Add(currentQuestion);
            }
        }
    }

    private async void OnSpeakClicked(object sender, EventArgs e)
    {
        if (currentQuestion == null || string.IsNullOrWhiteSpace(currentQuestion.Description)) return;

        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var trLocale = locales.FirstOrDefault(l => l.Language.StartsWith("tr", StringComparison.OrdinalIgnoreCase));

            SpeechOptions options = new()
            {
                Volume = 1.0f,
                Pitch = 1.0f,
                Locale = trLocale
            };

            await TextToSpeech.Default.SpeakAsync(currentQuestion.Description, options);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Seslendirme hatası: {ex.Message}");
        }
    }

    private void ShowFeedback(int index)
    {
        Label[] resLabels = [LblResult1, LblResult2, LblResult3, LblResult4];
        if (index is >= 0 and < 4) resLabels[index].IsVisible = true;
    }

    private void ResetOptionViews()
    {
        Label[] rbLabels = [Rb1, Rb2, Rb3, Rb4];
        Label[] resLabels = [LblResult1, LblResult2, LblResult3, LblResult4];

        for (int i = 0; i < 4; i++)
        {
            if (rbLabels[i] != null) rbLabels[i].Text = "○";
            if (resLabels[i] != null) resLabels[i].IsVisible = false;
        }
        if (BtnNext != null) BtnNext.IsVisible = false;
    }

    private void OnLanguageModeChanged(object sender, ToggledEventArgs e)
    {
        if (sender == null || e == null) return;
        isCodeToDescMode = e.Value;
        UpdateUIContent();
    }

    private void OnReviewModeChanged(object sender, CheckedChangedEventArgs e)
    {
        if (sender == null || e == null) return;
        isReviewMode = e.Value;
        StartNewGame();
    }

    private void OnAddToNotebookClicked(object sender, EventArgs e)
    {
        if (sender == null || e == null) return;
        if (currentQuestion != null && !notebookList.Contains(currentQuestion)) notebookList.Add(currentQuestion);
    }

    private async void OnOpenNotebookClicked(object sender, EventArgs e)
    {
        if (sender == null || e == null) return;
        if (notebookList.Count == 0) { await DisplayAlert("Defter", "Kaydedilen tarife pozisyon notu yok.", "Tamam"); return; }
        string content = "--- ÖZEL TARİFE NOTLARIM ---\n\n" + string.Join("\n\n", notebookList.Select(w => $"➤ {w.Code}\nTanım: {w.Description}\nBölüm: {w.Chapter}"));
        bool clear = await DisplayAlert("Notlarım", content, "TEMİZLE", "KAPAT");
        if (clear) notebookList.Clear();
    }

    private void OnToggleWordListClicked(object sender, EventArgs e)
    {
        if (LayoutAllWordsList == null || ColAllWords == null || e == null) return;

        if (LayoutAllWordsList.IsVisible)
        {
            LayoutAllWordsList.IsVisible = false;
            if (sender is Button btn) btn.Text = "TÜM LİSTE";
        }
        else
        {
            LayoutAllWordsList.IsVisible = true;
            displayedTariffs.Clear();
            foreach (var w in allTariffs) displayedTariffs.Add(w);
            if (sender is Button btn) btn.Text = "LİSTEYİ KAPAT";
        }
    }

    private void OnSearchWordChanged(object sender, TextChangedEventArgs e)
    {
        if (ColAllWords == null || sender == null || e == null) return;

        string query = e.NewTextValue?.Trim() ?? "";
        displayedTariffs.Clear();

        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var w in allTariffs) displayedTariffs.Add(w);
        }
        else
        {
            var filtered = allTariffs.Where(t =>
                (t.Description != null && t.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
                (t.Code != null && t.Code.Contains(query, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            foreach (var w in filtered) displayedTariffs.Add(w);
        }
    }

    // 01.01'DEN 24.04'E KADAR TÜM POZİSYONLAR
    private static List<CustomsTariff> GetMyTariffList()
    {
        return
        [
            // ==========================================
            // BÖLÜM I: CANLI HAYVANLAR VE HAYVANSAL ÜRÜNLER (FASIL 01 - 05)
            // ==========================================
            
            // FASIL 1: Canlı Hayvanlar
            new CustomsTariff("Canlı atlar, eşekler, katırlar ve bardolar", "01.01", "Bölüm 1"),
            new CustomsTariff("Canlı büyükbaş hayvanlar", "01.02", "Bölüm 1"),
            new CustomsTariff("Canlı domuzlar", "01.03", "Bölüm 1"),
            new CustomsTariff("Canlı koyun ve keçiler", "01.04", "Bölüm 1"),
            new CustomsTariff("Canlı kümes hayvanları (horoz, tavuk, ördek, kaz, hindi, beç tavuğu)", "01.05", "Bölüm 1"),
            new CustomsTariff("Diğer canlı hayvanlar (memeliler, sürüngenler, kuşlar, böcekler)", "01.06", "Bölüm 1"),

            // FASIL 2: Etler ve Yenilen Sakatat
            new CustomsTariff("Büyükbaş hayvanların eti (taze veya soğutulmuş)", "02.01", "Bölüm 1"),
            new CustomsTariff("Büyükbaş hayvanların eti (dondurulmuş)", "02.02", "Bölüm 1"),
            new CustomsTariff("Domuz eti (taze, soğutulmuş veya dondurulmuş)", "02.03", "Bölüm 1"),
            new CustomsTariff("Koyun ve keçi etleri (taze, soğutulmuş veya dondurulmuş)", "02.04", "Bölüm 1"),
            new CustomsTariff("At, eşek, katır veya bardo etleri (taze, soğutulmuş veya dondurulmuş)", "02.05", "Bölüm 1"),
            new CustomsTariff("Büyükbaş, domuz, koyun, keçi, at vb. hayvanların yenilen sakatatı", "02.06", "Bölüm 1"),
            new CustomsTariff("01.05 pozisyonundaki kümes hayvanlarının etleri ve yenilen sakatatı", "02.07", "Bölüm 1"),
            new CustomsTariff("Diğer hayvanların etleri ve yenilen sakatatı (tavşan, av hayvanları, kurbağa bacağı vb.)", "02.08", "Bölüm 1"),
            new CustomsTariff("Domuz yağı (etsiz) ve kümes hayvanı yağı (eritilmemiş/ekstrakte edilmemiş)", "02.09", "Bölüm 1"),
            new CustomsTariff("Tuzlanmış, salamura edilmiş, kurutulmuş veya tütsülenmiş et ve sakatat; et/sakatat unları", "02.10", "Bölüm 1"),

            // FASIL 3: Balıklar, Kabuklular, Yumuşakçalar ve Diğer Suda Yaşayan Omurgasızlar
            new CustomsTariff("Canlı balıklar", "03.01", "Bölüm 1"),
            new CustomsTariff("Taze veya soğutulmuş balıklar (03.04'teki balık filetoları hariç)", "03.02", "Bölüm 1"),
            new CustomsTariff("Dondurulmuş balıklar (03.04'teki balık filetoları hariç)", "03.03", "Bölüm 1"),
            new CustomsTariff("Balık filetoları ve diğer balık etleri (kıyılmış olsun olmasın; taze, soğutulmuş, dondurulmuş)", "03.04", "Bölüm 1"),
            new CustomsTariff("Kurutulmuş, tuzlanmış veya salamura edilmiş balıklar; tütsülenmiş balıklar; balık unları/peletleri", "03.05", "Bölüm 1"),
            new CustomsTariff("Kabuklu hayvanlar (kabuklu veya kabuksuz, canlı, taze, dondurulmuş, kurutulmuş, pişirilmiş vb.)", "03.06", "Bölüm 1"),
            new CustomsTariff("Yumuşakçalar (kabuklu veya kabuksuz, canlı, taze, dondurulmuş, kurutulmuş vb.)", "03.07", "Bölüm 1"),
            new CustomsTariff("Suda yaşayan diğer omurgasızlar (deniz hıyarları, deniz kestaneleri, deniz anaları vb.)", "03.08", "Bölüm 1"),
            new CustomsTariff("İnsan tüketimine uygun suda yaşayan omurgasızların unları, kaba unları ve peletleri", "03.09", "Bölüm 1"),

            // FASIL 4: Süt Ürünleri; Kuş Yumurtaları; Tabii Bal; Tarifenin Başka Yerinde Belirtilmeyen Hayvansal Ürünler
            new CustomsTariff("Süt ve krema (konsantre edilmemiş, ilave şeker veya diğer tatlandırıcı içermeyen)", "04.01", "Bölüm 1"),
            new CustomsTariff("Süt ve krema (konsantre edilmiş veya ilave şeker ya da diğer tatlandırıcı içeren)", "04.02", "Bölüm 1"),
            new CustomsTariff("Yayıkaltı, pıhtılaşmış süt ve krema, yoğurt, kefir ve diğer fermente edilmiş süt ürünleri", "04.03", "Bölüm 1"),
            new CustomsTariff("Peynir altı suyu ve tabii süt bileşenlerinden oluşan diğer ürünler", "04.04", "Bölüm 1"),
            new CustomsTariff("Tereyağı ve sütten elde edilen diğer katı ve sıvı yağlar; süt esaslı sürülebilir maddeler", "04.05", "Bölüm 1"),
            new CustomsTariff("Peynir ve pıhtılaşmış ürünler", "04.06", "Bölüm 1"),
            new CustomsTariff("Kuş ve kümes hayvanlarının yumurtaları (kabuklu, taze, korunmuş veya pişirilmiş)", "04.07", "Bölüm 1"),
            new CustomsTariff("Kuş yumurtaları (kabuksuz) ve yumurta sarıları (taze, kurutulmuş, pişirilmiş vb.)", "04.08", "Bölüm 1"),
            new CustomsTariff("Tabii bal", "04.09", "Bölüm 1"),
            new CustomsTariff("Tarifenin başka yerinde belirtilmeyen veya yer almayan hayvan menşeli yenilen ürünler (böcekler vb.)", "04.10", "Bölüm 1"),

            // FASIL 5: Tarifenin Başka Yerinde Belirtilmeyen Hayvansal Menşeli Diğer Ürünler
            new CustomsTariff("İnsan saçı (işlenmemiş, yıkanmış/yağı alınmış olsun olmasın); insan saçı döküntüleri", "05.01", "Bölüm 1"),
            new CustomsTariff("Domuz veya yaban domuzu kılları; porsuk kılları ve fırça yapımına mahsus diğer kıllar", "05.02", "Bölüm 1"),
            new CustomsTariff("Bağırsaklar, mesaneler ve mideler (balıklara ait olanlar hariç; bütün veya parça)", "05.04", "Bölüm 1"),
            new CustomsTariff("Kuş derileri ve tüyleri; tüy tozları ve döküntüleri", "05.05", "Bölüm 1"),
            new CustomsTariff("Kemikler ve boynuz göbekleri (işlenmemiş, yağı alınmış, asitle işlenmiş vb.); bunların toz ve döküntüleri", "05.06", "Bölüm 1"),
            new CustomsTariff("Fildişi, kaplumbağa kabuğu, balina çubuğu, boynuzlar, tırnaklar ve gagalar; bunların toz ve döküntüleri", "05.07", "Bölüm 1"),
            new CustomsTariff("Mercan ve benzeri maddeler; kabukluların veya yumuşakçaların kabukları; mürekkep balığı kemiği", "05.08", "Bölüm 1"),
            new CustomsTariff("Esmeramber, kunduz hayası, misk, civet; kantarit; safra; eczacılık ürünleri hazırlamada kullanılan hayvan salgıları", "05.10", "Bölüm 1"),
            new CustomsTariff("Tarifenin başka yerinde yer almayan hayvansal ürünler; insan tüketimine elverişli olmayan cansız hayvanlar", "05.11", "Bölüm 1"),

            // ==========================================
            // BÖLÜM II: BİTKİSEL ÜRÜNLER (FASIL 06 - 14)
            // ==========================================

            // FASIL 6: Canlı Ağaçlar ve Diğer Bitkiler; Yumrular, Kökler; Kesme Çiçekler ve Süs Yaprakları
            new CustomsTariff("Yumrular, soğanlar, yumrulu kökler, köksaplar (uyku halinde, vejetasyon veya çiçek açmış)", "06.01", "Bölüm 2"),
            new CustomsTariff("Diğer canlı bitkiler (kökleri dahil), çelikler ve aşı kalemleri; mantar miselleri", "06.02", "Bölüm 2"),
            new CustomsTariff("Kesme çiçekler ve çiçek tomurcukları (buket veya süsleme amaçlı, taze, kurutulmuş, boyanmış vb.)", "06.03", "Bölüm 2"),
            new CustomsTariff("Çiçeksiz yapraklar, dallar ve diğer bitki kısımları; süs amaçlı otlar ve yosunlar", "06.04", "Bölüm 2"),

            // FASIL 7: Yenilen Sebzeler ve Bazı Kök ve Yumrular
            new CustomsTariff("Patates (taze veya soğutulmuş)", "07.01", "Bölüm 2"),
            new CustomsTariff("Domates (taze veya soğutulmuş)", "07.02", "Bölüm 2"),
            new CustomsTariff("Soğanlar, şalotlar, sarımsaklar, pırasalar ve diğer allium türü sebzeler (taze veya soğutulmuş)", "07.03", "Bölüm 2"),
            new CustomsTariff("Lahanalar, karnabaharlar, alabaşlar, yaprak lahanalar ve benzeri brassica türü sebzeler", "07.04", "Bölüm 2"),
            new CustomsTariff("Marul (lactuca sativa) ve hindiba (cichorium türleri) (taze veya soğutulmuş)", "07.05", "Bölüm 2"),
            new CustomsTariff("Havuçlar, şalgamlar, kırmızı pancar, kereviz, turp ve benzeri yenilen kökler (taze veya soğutulmuş)", "07.06", "Bölüm 2"),
            new CustomsTariff("Hıyarlar ve kornişonlar (taze veya soğutulmuş)", "07.07", "Bölüm 2"),
            new CustomsTariff("Baklagiller (kabuklu veya kabuksuz; taze veya soğutulmuş)", "07.08", "Bölüm 2"),
            new CustomsTariff("Diğer sebzeler (taze veya soğutulmuş; enginar, kuşkonmaz, patlıcan, mantarlar, biberler vb.)", "07.09", "Bölüm 2"),
            new CustomsTariff("Dondurulmuş sebzeler (pişirilmemiş veya buharda/suda kaynatılarak pişirilmiş)", "07.10", "Bölüm 2"),
            new CustomsTariff("Geçici olarak konserve edilmiş sebzeler (derhal tüketilmeye uygun olmayanlar)", "07.11", "Bölüm 2"),
            new CustomsTariff("Kurutulmuş sebzeler (bütün, kesilmiş, dilimlenmiş, kırılmış veya toz halinde, başka işlem görmemiş)", "07.12", "Bölüm 2"),
            new CustomsTariff("Kurutulmuş baklagiller (kabuksuz, tanelenmiş, kırılmış veya ikiye ayrılmış olsun olmasın)", "07.13", "Bölüm 2"),
            new CustomsTariff("Manyok, ararot, salep, yer elması ve yüksek nişasta/inülin içeren benzeri kök ve yumrular; sago özü", "07.14", "Bölüm 2"),

            // FASIL 8: Yenilen Meyveler ve Yenilen Sert Kabuklu Meyveler; Turunçgillerin veya Kavunların Kabukları
            new CustomsTariff("Hindistan cevizi, brezilya cevizi ve kaju cevizi (taze veya kurutulmuş, kabuklu/kabuksuz)", "08.01", "Bölüm 2"),
            new CustomsTariff("Diğer sert kabuklu meyveler (badem, fındık, ceviz, kestane, antep fıstığı vb.)", "08.02", "Bölüm 2"),
            new CustomsTariff("Muz (plantain dahil, taze veya kurutulmuş)", "08.03", "Bölüm 2"),
            new CustomsTariff("Hurma, incir, ananas, avokado, guava, mango ve mangostan (taze veya kurutulmuş)", "08.04", "Bölüm 2"),
            new CustomsTariff("Turunçgiller (portakal, mandalina, limon, greyfurt vb.; taze veya kurutulmuş)", "08.05", "Bölüm 2"),
            new CustomsTariff("Üzümler (taze veya kurutulmuş/kuru üzüm)", "08.06", "Bölüm 2"),
            new CustomsTariff("Kavunlar (karpuz dahil) ve papaya (taze)", "08.07", "Bölüm 2"),
            new CustomsTariff("Elma, armut ve ayva (taze)", "08.08", "Bölüm 2"),
            new CustomsTariff("Kayısı, kiraz, şeftali (nektarin dahil), erik ve çakal eriği (taze)", "08.09", "Bölüm 2"),
            new CustomsTariff("Diğer taze meyveler (çilek, ahududu, kivi, nar vb.)", "08.10", "Bölüm 2"),
            new CustomsTariff("Dondurulmuş meyveler ve sert kabuklu meyveler (ilave şeker içersin içermesin)", "08.11", "Bölüm 2"),
            new CustomsTariff("Geçici olarak konserve edilmiş meyveler ve sert kabuklu meyveler", "08.12", "Bölüm 2"),
            new CustomsTariff("Kurutulmuş meyveler (08.01-08.06 hariç); bu fasıldaki meyve ve sert kabuklu meyve karışımları", "08.13", "Bölüm 2"),
            new CustomsTariff("Turunçgillerin veya kavunların (karpuz dahil) kabukları (taze, dondurulmuş, kurutulmuş vb.)", "08.14", "Bölüm 2"),

            // FASIL 9: Kahve, Çay, Paraguay Çayı (Mate) ve Baharat
            new CustomsTariff("Kahve (kavrulmuş veya kafeini alınmış olsun olmasın); kahve kabuk ve zarları; kahve yerine geçen maddeler", "09.01", "Bölüm 2"),
            new CustomsTariff("Çay (aromalı olsun olmasın)", "09.02", "Bölüm 2"),
            new CustomsTariff("Paraguay çayı (mate)", "09.03", "Bölüm 2"),
            new CustomsTariff("Biber (piper cinsi); capsicum veya pimenta cinsi meyveler (kurutulmuş, ezilmiş veya öğütülmüş)", "09.04", "Bölüm 2"),
            new CustomsTariff("Vanilya", "09.05", "Bölüm 2"),
            new CustomsTariff("Tarçın ve tarçın ağacı çiçekleri", "09.06", "Bölüm 2"),
            new CustomsTariff("Karanfil (bütün meyveler, karanfiller ve saplar)", "09.07", "Bölüm 2"),
            new CustomsTariff("Küçük hindistan cevizi, kabuğu (masis) ve kakuleler", "09.08", "Bölüm 2"),
            new CustomsTariff("Anason, yıldız anasonu, rezene, kişniş, kimyon veya Frenk kimyonu tohumları; ardıç meyveleri", "09.09", "Bölüm 2"),
            new CustomsTariff("Zencefil, safran, zerdeçal (kurkuma), kekik, defne yaprakları, köri ve diğer baharatlar", "09.10", "Bölüm 2"),

            // FASIL 10: Hububat
            new CustomsTariff("Buğday ve mahlut", "10.01", "Bölüm 2"),
            new CustomsTariff("Çavdar", "10.02", "Bölüm 2"),
            new CustomsTariff("Arpa", "10.03", "Bölüm 2"),
            new CustomsTariff("Yulaf", "10.04", "Bölüm 2"),
            new CustomsTariff("Mısır", "10.05", "Bölüm 2"),
            new CustomsTariff("Pirinç", "10.06", "Bölüm 2"),
            new CustomsTariff("Dane süpürge darısı (sorgum)", "10.07", "Bölüm 2"),
            new CustomsTariff("Karabuğday, darı ve kuşyemi; diğer hububat (kinoa vb.)", "10.08", "Bölüm 2"),

            // FASIL 11: Değirmencilik Ürünleri; Malt; Nişasta; İnülin; Buğday Gluteni
            new CustomsTariff("Buğday unu veya mahlut unu", "11.01", "Bölüm 2"),
            new CustomsTariff("Hububat unları (buğday veya mahlut unu hariç; mısır unu, çavdar unu vb.)", "11.02", "Bölüm 2"),
            new CustomsTariff("Hububat taneleri (yarma, kaba un ve peletler)", "11.03", "Bölüm 2"),
            new CustomsTariff("İşlenmiş diğer hububat taneleri (kabuksuz, ezilmiş, flokon, yuvarlatılmış, kavuzlanmış vb.); hububat tohumu özü", "11.04", "Bölüm 2"),
            new CustomsTariff("Kuru baklagillerin, sagonun, kök veya yumruların (07.14) veya Fasıl 8 ürünlerinin unları ve ezmeleri", "11.05", "Bölüm 2"),
            new CustomsTariff("Malt (kavrulmuş olsun olmasın)", "11.07", "Bölüm 2"),
            new CustomsTariff("Nişastalar; inülin", "11.08", "Bölüm 2"),
            new CustomsTariff("Buğday gluteni (kurutulmuş olsun olmasın)", "11.09", "Bölüm 2"),

            // FASIL 12: Yağlı Tohum ve Meyveler; Muhtelif Tane, Tohum ve Meyveler; Sanayi Bitkileri; Saman ve Kaba Yem
            new CustomsTariff("Soya fasulyesi (kırılmış olsun olmasın)", "12.01", "Bölüm 2"),
            new CustomsTariff("Yer fıstığı (kavrulmamış veya pişirilmemiş, kabuklu/kabuksuz)", "12.02", "Bölüm 2"),
            new CustomsTariff("Kopra", "12.03", "Bölüm 2"),
            new CustomsTariff("Keten tohumu (kırılmış olsun olmasın)", "12.04", "Bölüm 2"),
            new CustomsTariff("Kolza veya kolza tohumları (kırılmış olsun olmasın)", "12.05", "Bölüm 2"),
            new CustomsTariff("Ayçiçeği tohumu (kırılmış olsun olmasın)", "12.06", "Bölüm 2"),
            new CustomsTariff("Diğer yağlı tohumlar ve meyveler (pamuk, susam, hardal, haşhaş tohumu vb.)", "12.07", "Bölüm 2"),
            new CustomsTariff("Yağlı tohum ve meyvelerin unları ve kaba unları (hardal unu hariç)", "12.08", "Bölüm 2"),
            new CustomsTariff("Ekim amacıyla kullanılan tohum, meyve ve sporlar", "12.09", "Bölüm 2"),
            new CustomsTariff("Şerbetçi otu kozalağı (taze veya kurutulmuş, toz/pelet halinde); lüpülin", "12.10", "Bölüm 2"),
            new CustomsTariff("Bitkiler ve kısımları (parfümeri, eczacılık, böcek öldürücü vb. amaçlarla kullanılanlar)", "12.11", "Bölüm 2"),
            new CustomsTariff("Keçiboynuzu, deniz yosunları ve diğer algler, şeker pancarı ve şeker kamışı; meyve çekirdekleri", "12.12", "Bölüm 2"),
            new CustomsTariff("Hububat samanı ve kavuzu (işlenmemiş)", "12.13", "Bölüm 2"),
            new CustomsTariff("Şalgam, yemlik pancar, yem kökleri, ot, yonca, korunga, yemlik lahana, acı bakla ve benzeri yemler", "12.14", "Bölüm 2"),

            // FASIL 13: Saklar, Reçineler ve Diğer Bitkisel Özsu ve Hülasalar
            new CustomsTariff("Lak; tabii saklar, reçineler, sakız-reçineler ve oleoreçineler (örneğin balzamlar)", "13.01", "Bölüm 2"),
            new CustomsTariff("Bitkisel özsu ve hülasalar; pektik maddeler, pektinatlar ve pektatlar; agarlar ve diğer bitkisel kıvam vericiler", "13.02", "Bölüm 2"),

            // FASIL 14: Örülmeye Elverişli Bitkisel Maddeler; Tarifenin Başka Yerinde Yer Almayan Bitkisel Ürünler
            new CustomsTariff("Örülmeye elverişli bitkisel maddeler (bambu, hintkamışı, kamış, saz, söğüt, rafya vb.)", "14.01", "Bölüm 2"),
            new CustomsTariff("Tarifenin başka yerinde belirtilmeyen veya yer almayan bitkisel ürünler (pamuk linteri vb.)", "14.04", "Bölüm 2"),

            // ==========================================
            // BÖLÜM III: HAYVANSAL, BİTKİSEL VEYA MİKROBİYAL KATI VE SIVI YAĞLAR (FASIL 15)
            // ==========================================

            // FASIL 15: Hayvansal, Bitkisel veya Mikrobiyal Katı ve Sıvı Yağlar; Yenilen Katı Yağlar; Mumlar
            new CustomsTariff("Domuz yağı (lardon dahil) ve kümes hayvanları yağı (02.09 veya 15.03 hariç)", "15.01", "Bölüm 3"),
            new CustomsTariff("Büyükbaş, koyun veya keçi yağları (15.03 hariç)", "15.02", "Bölüm 3"),
            new CustomsTariff("Lardstearin, lard sıvı yağı, oleostearin, oleomargarin ve donyağı sıvı yağı", "15.03", "Bölüm 3"),
            new CustomsTariff("Balıkların veya deniz memelilerinin katı ve sıvı yağları ve bunların fraksiyonları", "15.04", "Bölüm 3"),
            new CustomsTariff("Yapağı yağı ve bundan elde edilen yağlı maddeler (lanolin dahil)", "15.05", "Bölüm 3"),
            new CustomsTariff("Diğer hayvansal katı ve sıvı yağlar ve bunların fraksiyonları", "15.06", "Bölüm 3"),
            new CustomsTariff("Soya fasulyesi yağı ve fraksiyonları (rafine edilmiş olsun olmasın)", "15.07", "Bölüm 3"),
            new CustomsTariff("Yer fıstığı yağı ve fraksiyonları (rafine edilmiş olsun olmasın)", "15.08", "Bölüm 3"),
            new CustomsTariff("Zeytinyağı ve fraksiyonları (rafine edilmiş olsun olmasın, kimyasal olarak değiştirilmemiş)", "15.09", "Bölüm 3"),
            new CustomsTariff("Zeytinden elde edilen diğer katı ve sıvı yağlar (pirina yağı dahil) ve bunların fraksiyonları", "15.10", "Bölüm 3"),
            new CustomsTariff("Palmiye yağı ve fraksiyonları (rafine edilmiş olsun olmasın)", "15.11", "Bölüm 3"),
            new CustomsTariff("Ayçiçeği tohumu, aspir veya pamuk tohumu yağları ve fraksiyonları", "15.12", "Bölüm 3"),
            new CustomsTariff("Hindistan cevizi (kopra), palmiye çekirdeği veya babassu yağı ve fraksiyonları", "15.13", "Bölüm 3"),
            new CustomsTariff("Kolza, kolza tohumu veya hardal yağı ve fraksiyonları", "15.14", "Bölüm 3"),
            new CustomsTariff("Diğer sabit bitkisel veya mikrobiyal katı ve sıvı yağlar (keten, mısır, susam yağı vb.)", "15.15", "Bölüm 3"),
            new CustomsTariff("Hayvansal, bitkisel veya mikrobiyal katı ve sıvı yağlar (kısmen/tamamen hidrojene edilmiş, interesterifiye vb.)", "15.16", "Bölüm 3"),
            new CustomsTariff("Margarin; bu fasıldaki katı ve sıvı yağların yenilen karışımları veya müstahzarları", "15.17", "Bölüm 3"),
            new CustomsTariff("Pişirilmiş, oksitlenmiş, dehidrate edilmiş veya kimyasal olarak değiştirilmiş yağlar; yenilmeyen karışımlar", "15.18", "Bölüm 3"),
            new CustomsTariff("Gliserol (ham gliserin); gliserinli sular ve gliserinli liler", "15.20", "Bölüm 3"),
            new CustomsTariff("Bitkisel mumlar (trigliseritler hariç), balmumu, diğer böcek mumları ve ispermeçet", "15.21", "Bölüm 3"),
            new CustomsTariff("Degra; yağlı maddelerin veya hayvansal/bitkisel mumların işlenmesinden arta kalan kalıntılar", "15.22", "Bölüm 3"),

            // ==========================================
            // BÖLÜM IV: GIDA SANAYİİ MÜSTAHZARLARI; MEŞRUBAT, İÇKİLER VE SİRKE; TÜTÜN (FASIL 16 - 24)
            // ==========================================

            // FASIL 16: Et, Balık, Kabuklular, Yumuşakçalar veya Diğer Suda Yaşayan Omurgasızların Müstahzarları
            new CustomsTariff("Sosisler, sucuklar ve benzeri ürünler (et, sakatat veya kandan yapılan); bu ürünlerden gıda müstahzarları", "16.01", "Bölüm 4"),
            new CustomsTariff("Hazırlanmış veya konserve edilmiş diğer et, sakatat veya kan", "16.02", "Bölüm 4"),
            new CustomsTariff("Et, balık veya kabuklular, yumuşakçalar ya da diğer su omurgasızlarının hülasaları ve suları", "16.03", "Bölüm 4"),
            new CustomsTariff("Hazırlanmış veya konserve edilmiş balıklar; havyar ve balık yumurtalarından hazırlanan havyar yerine geçenler", "16.04", "Bölüm 4"),
            new CustomsTariff("Hazırlanmış veya konserve edilmiş kabuklular, yumuşakçalar ve diğer suda yaşayan omurgasızlar", "16.05", "Bölüm 4"),

            // FASIL 17: Şeker ve Şeker Mamulleri
            new CustomsTariff("Kamış veya pancar şekeri ve kimyaca saf sakaroz (katı halde)", "17.01", "Bölüm 4"),
            new CustomsTariff("Diğer şekerler (kimyaca saf laktoz, maltoz, glukoz ve fruktoz dahil); şeker şurupları; suni bal; karamel", "17.02", "Bölüm 4"),
            new CustomsTariff("Şekerin ekstrakte edilmesi veya tasfiyesinden arta kalan melaslar", "17.03", "Bölüm 4"),
            new CustomsTariff("Şeker mamulleri (beyaz çikolata dahil, kakao içermeyenler)", "17.04", "Bölüm 4"),

            // FASIL 18: Kakao ve Kakao Müstahzarları
            new CustomsTariff("Kakao dane ve kırıkları (ham veya kavrulmuş)", "18.01", "Bölüm 4"),
            new CustomsTariff("Kakao kabukları, zarları ve diğer kakao döküntüleri", "18.02", "Bölüm 4"),
            new CustomsTariff("Kakao hamuru (yağı alınmış olsun olmasın)", "18.03", "Bölüm 4"),
            new CustomsTariff("Kakao yağı ve sıvı yağı", "18.04", "Bölüm 4"),
            new CustomsTariff("Kakao tozu (ilave şeker veya diğer tatlandırıcı içermeyen)", "18.05", "Bölüm 4"),
            new CustomsTariff("Çikolata ve kakao içeren diğer gıda müstahzarları", "18.06", "Bölüm 4"),

            // FASIL 19: Hububat, Un, Nişasta veya Süt Müstahzarları; Pastacılık Ürünleri
            new CustomsTariff("Malt hülasası; un, kaba un, nişasta veya süt esaslı gıda müstahzarları (kakao içermeyen veya az içeren)", "19.01", "Bölüm 4"),
            new CustomsTariff("Makarnalar ve şehriyeler (pişirilmiş veya doldurulmuş olsun olmasın); kuskus", "19.02", "Bölüm 4"),
            new CustomsTariff("Tapyoka ve nişastadan hazırlanan tapyoka benzerleri (flokon, taneli, yuvarlak vb.)", "19.03", "Bölüm 4"),
            new CustomsTariff("Hububat veya hububat ürünlerinin kabartılması veya kavrulmasıyla elde edilen gıdalar (corn flakes vb.)", "19.04", "Bölüm 4"),
            new CustomsTariff("Ekmek, pasta, kek, bisküvi ve diğer fırıncılık mamulleri; boş ilaç kapsülleri, mühür güllacı, pirinç kağıdı", "19.05", "Bölüm 4"),

            // FASIL 20: Sebzeler, Meyveler, Sert Kabuklu Meyveler ve Bitkilerin Diğer Kısımlarının Müstahzarları
            new CustomsTariff("Sirke veya asetik asitle hazırlanmış veya konserve edilmiş sebzeler, meyveler ve bitki kısımları", "20.01", "Bölüm 4"),
            new CustomsTariff("Sirke veya asetik asit uygulanmaksızın hazırlanmış domatesler (salça, soyulmuş domates vb.)", "20.02", "Bölüm 4"),
            new CustomsTariff("Sirke veya asetik asit uygulanmaksızın hazırlanmış mantarlar ve trüfler", "20.03", "Bölüm 4"),
            new CustomsTariff("Sirke uygulanmaksızın hazırlanmış diğer sebzeler (dondurulmuş)", "20.04", "Bölüm 4"),
            new CustomsTariff("Sirke uygulanmaksızın hazırlanmış diğer sebzeler (dondurulmamış; patates cipsi, zeytin vb.)", "20.05", "Bölüm 4"),
            new CustomsTariff("Şekerle konserve edilmiş sebzeler, meyveler, meyve kabukları ve bitki parçaları (glase veya kristalize)", "20.06", "Bölüm 4"),
            new CustomsTariff("Reçeller, jöleler, marmelatlar, meyve püresi ve pastları (pişirilerek hazırlanmış)", "20.07", "Bölüm 4"),
            new CustomsTariff("Başka surette hazırlanmış veya konserve edilmiş meyveler, sert kabuklu meyveler (fıstık ezmesi vb.)", "20.08", "Bölüm 4"),
            new CustomsTariff("Meyve suları (üzüm şırası dahil) ve sebze suları (fermente edilmemiş ve alkolsüz)", "20.09", "Bölüm 4"),

            // FASIL 21: Çeşitli Yenilen Gıda Müstahzarları
            new CustomsTariff("Kahve, çay veya mate hülasaları, esansları ve konsantreleri; hindiba ve kavrulmuş kahve yerine geçenler", "21.01", "Bölüm 4"),
            new CustomsTariff("Mayalar (canlı veya cansız); diğer tek hücreli mikroorganizmalar (cansız); hazırlanmış kabartma tozları", "21.02", "Bölüm 4"),
            new CustomsTariff("Soslar ve bunların müstahzarları; çeşni ve lezzet verici karışımlar; hardal unu ve hazırlanmış hardal", "21.03", "Bölüm 4"),
            new CustomsTariff("Çorbalar, et suları ve bunların müstahzarları; homojenize edilmiş karma gıda müstahzarları", "21.04", "Bölüm 4"),
            new CustomsTariff("Dondurmalar ve yenilen diğer buzlar (kakao içersin içermesin)", "21.05", "Bölüm 4"),
            new CustomsTariff("Tarifenin başka yerinde belirtilmeyen veya yer almayan gıda müstahzarları (protein konsantreleri, şuruplar vb.)", "21.06", "Bölüm 4"),

            // FASIL 22: Meşrubat, Alkollü İçkiler ve Sirke
            new CustomsTariff("Sular (tabii veya suni maden suları ve gazlı sular dahil; ilave şeker veya tatlandırıcı içermeyen); buz ve kar", "22.01", "Bölüm 4"),
            new CustomsTariff("Sular (ilave şeker veya tatlandırıcı içeren veya aromalandırılmış) ve diğer alkolsüz içecekler (enerji içeceği vb.)", "22.02", "Bölüm 4"),
            new CustomsTariff("Malttan üretilen biralar", "22.03", "Bölüm 4"),
            new CustomsTariff("Taze üzüm şarapları (kuvvetlendirilmiş şaraplar dahil); üzüm şırası (20.09 hariç)", "22.04", "Bölüm 4"),
            new CustomsTariff("Vermut ve taze üzümden yapılan bitki veya aromatik maddelerle kokulandırılmış diğer şaraplar", "22.05", "Bölüm 4"),
            new CustomsTariff("Diğer fermente edilmiş içkiler (elma şarabı/sidre, armut şarabı, bal şarabı/met vb.); bunların karışımları", "22.06", "Bölüm 4"),
            new CustomsTariff("Hacimce %80 veya daha fazla alkol derecesine sahip tağyir edilmemiş etil alkol; tağyir edilmiş her derecede etil alkol", "22.07", "Bölüm 4"),
            new CustomsTariff("Hacimce %80'den az alkol derecesine sahip etil alkol; damıtık alkollü içkiler, likörler ve diğer alkollü içecekler (rakı, votka, viski vb.)", "22.08", "Bölüm 4"),
            new CustomsTariff("Sirke ve asetik asitten elde edilen sirke yerine geçen maddeler", "22.09", "Bölüm 4"),

            // FASIL 23: Gıda Sanayiinin Kalıntı ve Döküntüleri; Hayvanlar İçin Hazırlanmış Kaba Yemler
            new CustomsTariff("Et, sakatat, balık, kabuklu veya yumuşakçalardan elde edilen unlar, kaba unlar ve peletler; kıkırdaklar", "23.01", "Bölüm 4"),
            new CustomsTariff("Hububatın veya baklagillerin elenmesi, öğütülmesi veya diğer işlemlerinden arta kalan kepek, kavuz ve kalıntılar", "23.02", "Bölüm 4"),
            new CustomsTariff("Nişastacılık kalıntıları ve benzeri artıklar; pancar posası, bagas ve şekercilik sanayii artıkları; biracılık/damıtma artıkları", "23.03", "Bölüm 4"),
            new CustomsTariff("Soya fasulyesi yağının çıkarılmasından arta kalan küspe ve diğer katı artıklar", "23.04", "Bölüm 4"),
            new CustomsTariff("Yer fıstığı yağının çıkarılmasından arta kalan küspe ve diğer katı artıklar", "23.05", "Bölüm 4"),
            new CustomsTariff("Diğer bitkisel veya mikrobiyal katı/sıvı yağların çıkarılmasından arta kalan küspeler ve katı artıklar (ayçiçeği, pamuk küspesi vb.)", "23.06", "Bölüm 4"),
            new CustomsTariff("Şarap tortusu; ham şarap taşı", "23.07", "Bölüm 4"),
            new CustomsTariff("Hayvan gıdası olarak kullanılan bitkisel maddeler ve atıklar (başka yerde belirtilmeyen)", "23.08", "Bölüm 4"),
            new CustomsTariff("Hayvan yemlemesinde kullanılan müstahzarlar (kedi/köpek mamaları, premiksler vb.)", "23.09", "Bölüm 4"),

            // FASIL 24: Tütün ve Tütün Yerine Geçen İşlenmiş Maddeler; Nikotin İçeren veya İçermeyen Ürünler
            new CustomsTariff("Yaprak tütün ve tütün döküntüleri (işlenmemiş)", "24.01", "Bölüm 4"),
            new CustomsTariff("Purolar, uçları açık purolar, sigarillolar ve sigaralar (tütünden veya tütün yerine geçen maddelerden)", "24.02", "Bölüm 4"),
            new CustomsTariff("Diğer mamul tütün ve tütün yerine geçen maddeler; homojenize/yeniden tertip edilmiş tütün; tütün hülasaları ve esansları (nargile tütünü, pipo tütünü vb.)", "24.03", "Bölüm 4"),
            new CustomsTariff("Yanma olmadan solunan ürünler (ısıtılan tütün ürünleri, e-sigara likitleri) ve nikotin içeren diğer ürünler", "24.04", "Bölüm 4")
        ];
    }
}