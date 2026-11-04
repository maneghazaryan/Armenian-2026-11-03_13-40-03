// Browsers keep files written to Application.persistentDataPath in memory until they are
// synced to IndexedDB. Called after every progress save so a reload never loses progress.
mergeInto(LibraryManager.library, {
  AibSyncFs: function () {
    FS.syncfs(false, function (err) {
      if (err) console.warn("Ayb: could not save progress", err);
    });
  },
});
