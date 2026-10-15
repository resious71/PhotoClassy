using PhotoClassy.Core.Engine;

namespace PhotoClassy.Services
{
    public interface IDialogService
    {
        /// <summary>폴더 선택 대화상자. 취소하면 null.</summary>
        string PickFolder(string description, string initialFolder);

        /// <summary>로그 저장 경로 선택. 취소하면 null.</summary>
        string PickLogSavePath(string defaultFileName);

        void ShowInfo(string message);

        void ShowWarning(string message);

        void ShowError(string message);

        bool Confirm(string message);

        /// <summary>중복 파일 삭제 확인. "삭제"를 누르면 true, "보관"이면 false.</summary>
        bool AskDeleteDuplicates(int duplicateCount, string duplicatesFolder);

        /// <summary>결과 요약 화면.</summary>
        void ShowSummary(ClassificationResult result, System.Action saveLog, System.Action openTarget);
    }
}
