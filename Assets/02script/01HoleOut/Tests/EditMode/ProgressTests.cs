using HoleOut.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace HoleOut.Tests
{
    public class ProgressTests
    {
        const string Id = "HoleOut.Progress.LevelId", Index = "HoleOut.Progress.Level";
        string _id;
        int _index;
        bool _hadId, _hadIndex;

        [SetUp]
        public void Save()
        {
            _hadId = PlayerPrefs.HasKey(Id); _id = PlayerPrefs.GetString(Id, "");
            _hadIndex = PlayerPrefs.HasKey(Index); _index = PlayerPrefs.GetInt(Index, 0);
        }

        [TearDown]
        public void Restore()
        {
            if (_hadId) PlayerPrefs.SetString(Id, _id); else PlayerPrefs.DeleteKey(Id);
            if (_hadIndex) PlayerPrefs.SetInt(Index, _index); else PlayerPrefs.DeleteKey(Index);
        }

        [Test]
        public void Resolve_FindsSavedLevelAfterReorder()
        {
            PlayerProgress.SaveNext(1, "b");
            Assert.AreEqual(2, PlayerProgress.ResolveIndex(new[] { "x", "a", "b" }), "Chèn màn mới phía trước vẫn vào đúng màn.");
        }

        [Test]
        public void Resolve_FallsBackToIndexWhenLevelRemoved()
        {
            PlayerProgress.SaveNext(4, "gone");
            Assert.AreEqual(1, PlayerProgress.ResolveIndex(new[] { "a", "b", "c" }), "Không thấy mã màn thì dùng chỉ số vòng lại.");
        }
    }
}
