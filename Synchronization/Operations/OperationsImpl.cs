using Synchronization.Extensions;

namespace Synchronization.Operations
{
    public class OperationImpl : IOperations
	{
        private DirectoryInfo _source = null!;
        private DirectoryInfo _target = null!;
        private bool _dryRun;
        private bool _allowEmptySource;
        private string _sourceReadyFile = ".sync-ready";

        public OperationImpl InitializeOperation(
            string source,
            string target,
            bool dryRun = false,
            bool allowEmptySource = false,
            string sourceReadyFile = ".sync-ready")
		{
            _source = new DirectoryInfo(source);
            _target = new DirectoryInfo(target);
            _dryRun = dryRun;
            _allowEmptySource = allowEmptySource;
            _sourceReadyFile = sourceReadyFile;
            OperationExtensions.ValidatePaths(_source, _target);
            OperationExtensions.ValidateReadyFilePath(_source, _sourceReadyFile);
            OperationExtensions.ValidateSourceReady(_source, _sourceReadyFile);

            return this;
        }

        public OperationImpl Copy()
        {
            _source.Copy(_target, _dryRun, _sourceReadyFile, _allowEmptySource);
            return this;
        }

        public OperationImpl Delete()
        {
            _source.Delete(_target, _dryRun, _allowEmptySource, _sourceReadyFile);
            return this;
        }

        public OperationImpl Update()
        {
            _source.Update(_target, _dryRun, _sourceReadyFile, _allowEmptySource);
            return this;
        }
    }
}
