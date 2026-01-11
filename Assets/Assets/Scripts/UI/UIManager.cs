using UnityEngine;
using UnityEngine.UI;

namespace Idle
{
    public sealed class UIManager : MonoBehaviour
    {
        //enum
        public enum ShopScreenType { Closed, CityScreen, ResidentsScreen, ComfortScreen, AutomationScreen }

        [Header("Screens")]
        public GameObject CityScreen;
        public GameObject ResidentsScreen;
        public GameObject ComfortScreen;
        public GameObject AutomationScreen;

        [Header("Text")]
        public Text MoneyText;
        public Text MoneyPerSecondText;

        [Header("Gold & Score Display")]
        public Text GoldText; // ゴールド表示用テキスト
        public Text ScoreText; // スコア表示用テキスト

        [Header("Bounce Animation Settings")]
        public float punchScale = 1.3f; // バウンド時の拡大率
        public float animationSpeed = 8.0f; // アニメーション速度

        ShopScreenType shopScreenType;        //Enum for tracking open store type
        bool isPause;                         //Check pause status

        // バウンド演出用の元のスケール
        private Vector3 goldOriginalScale;
        private Vector3 scoreOriginalScale;

        private void Start()
        {
            // 元のスケールを保存
            if (GoldText != null) goldOriginalScale = GoldText.transform.localScale;
            if (ScoreText != null) scoreOriginalScale = ScoreText.transform.localScale;

            UpdateUI();
        }

        private void Update()
        {
            // ゴールドテキストのバウンドアニメーション（元のサイズに戻す）
            if (GoldText != null)
            {
                GoldText.transform.localScale = Vector3.Lerp(
                    GoldText.transform.localScale,
                    goldOriginalScale,
                    Time.deltaTime * animationSpeed
                );
            }

            // スコアテキストのバウンドアニメーション（元のサイズに戻す）
            if (ScoreText != null)
            {
                ScoreText.transform.localScale = Vector3.Lerp(
                    ScoreText.transform.localScale,
                    scoreOriginalScale,
                    Time.deltaTime * animationSpeed
                );
            }
        }

        //Update UI Total
        public void UpdateUI()
        {
            //Assign text
            MoneyText.text = IntParseToString(DataManager.data.Money);
            MoneyPerSecondText.text = "+" + IntParseToString(DataManager.data.MoneyPerSecond) + " /s";


            //Update upgrades UI
            Managers.Instance.upgradeManager.UpdateUI();

            //Save data to file
            DataManager.SaveData();
        }

        /// <summary>
        /// ゴールドとスコアを更新（バウンド演出付き）
        /// </summary>
        /// <param name="gold">現在のゴールド</param>
        /// <param name="score">現在のスコア</param>
        public void UpdateGoldAndScore(long gold, int score)
        {
            // ゴールド表示更新
            if (GoldText != null)
            {
                GoldText.text = "GOLD: " + gold.ToString("N0"); // 3桁区切りで表示

                // ★バウンド演出：テキストを拡大
                GoldText.transform.localScale = goldOriginalScale * punchScale;
            }

            // スコア表示更新
            if (ScoreText != null)
            {
                ScoreText.text = "SCORE: " + score.ToString();

                // ★バウンド演出：テキストを拡大
                ScoreText.transform.localScale = scoreOriginalScale * punchScale;
            }
        }

        //Convert number to string with zero replaced by letters
        public static string IntParseToString(long value)
        {
            string result = value.ToString();

            if (value >= 1000)
            {
                result = Mathf.Floor(((float)value / 100)) / 10 + " k";
            }

            if (value >= 1000000)
            {
                result = Mathf.Floor(((float)value / 10000)) / 100 + " mi";
            }

            if (value >= 1000000000)
            {
                result = Mathf.Floor(((float)value / 10000000)) / 100 + " bi";
            }

            if (value >= 1000000000000)
            {
                result = Mathf.Floor(((float)value / 1000000000)) / 1000 + " qua";
            }

            return result;
        }

        #region //Shop
        //The method invokes the opening of the store
        public void ClickShop(string screenName)
        {
            switch (screenName)
            {
                case "CityScreen":
                    //Call open method
                    ChangeShopScreen(ShopScreenType.CityScreen);
                    break;
                case "ResidentsScreen":
                    ChangeShopScreen(ShopScreenType.ResidentsScreen);
                    break;
                case "ComfortScreen":
                    ChangeShopScreen(ShopScreenType.ComfortScreen);
                    break;
                case "AutomationScreen":
                    ChangeShopScreen(ShopScreenType.AutomationScreen);
                    break;
            }

        }

        public void ChangeShopScreen(ShopScreenType shopScreenType)
        {
            //If shopScreenType is equal to the same Screen that we ship, the store will be closed
            if (shopScreenType == this.shopScreenType)
            {
                //Calling a method to play an animation from AnimatorManager, by calling Singleton, store collapse animation
                Managers.Instance.animatorManager.PlayAnimation(Managers.Instance.animatorManager.shopAnimator, "ShopHide");
                //set the class variable shopScreenType to closed
                this.shopScreenType = ShopScreenType.Closed;
            }
            else
            {
                //Call the method of closing all screens for further replacement.
                CloseShopScreens();
                switch (shopScreenType)
                {
                    case ShopScreenType.CityScreen:
                        //set the class variable shopScreenType to local shopScreenType
                        this.shopScreenType = shopScreenType;
                        //Turn on the screen
                        CityScreen.SetActive(true);
                        break;
                    case ShopScreenType.ResidentsScreen:
                        this.shopScreenType = shopScreenType;
                        ResidentsScreen.SetActive(true);
                        break;
                    case ShopScreenType.ComfortScreen:
                        this.shopScreenType = shopScreenType;
                        ComfortScreen.SetActive(true);
                        break;
                    case ShopScreenType.AutomationScreen:
                        this.shopScreenType = shopScreenType;
                        AutomationScreen.SetActive(true);
                        break;
                }

                //Store open animation
                Managers.Instance.animatorManager.PlayAnimation(Managers.Instance.animatorManager.shopAnimator, "ShopShow");
            }
        }

        //It is necessary to close all stores, previously called from ChangeShopScreen
        void CloseShopScreens()
        {
            //Just turn off all screens, nothing ordinary :)
            CityScreen.SetActive(false);
            ResidentsScreen.SetActive(false);
            ComfortScreen.SetActive(false);
            AutomationScreen.SetActive(false);
        }
        #endregion
        #region//Screens
        //Method for changing main screens
        public void ChangeScreen(string screenName)
        {
            switch (screenName)
            {
                case "GameScreen":
                    Managers.Instance.animatorManager.PlayAnimation(Managers.Instance.animatorManager.screensAnimator, "GameScreen");
                    break;

                case "PauseMenu":
                    if (!isPause)
                    {
                        Managers.Instance.animatorManager.PlayAnimation(Managers.Instance.animatorManager.screensAnimator, "PauseMenuShow");
                        isPause = true;
                    }
                    else
                    {
                        Managers.Instance.animatorManager.PlayAnimation(Managers.Instance.animatorManager.screensAnimator, "PauseMenuHide");
                        isPause = false;
                    }
                    break;
            }
        }
        #endregion

    }
}