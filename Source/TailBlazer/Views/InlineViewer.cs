using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using DynamicData.Binding;
using TailBlazer.Domain.FileHandling;
using TailBlazer.Domain.Infrastructure;
using TailBlazer.Infrastucture;

namespace TailBlazer.Views
{
    public class InlineViewer : AbstractNotifyPropertyChanged, IScrollReceiver,IDisposable
    {
        private readonly IDisposable _cleanUp;
        private readonly ReadOnlyObservableCollection<LineProxy> _data;
        private readonly ILineScroller _lineScroller;
        private readonly ISubject<ScrollRequest> _userScrollRequested = new ReplaySubject<ScrollRequest>(1);

        private int _firstIndex;
        private int _pageSize;
        public ReadOnlyObservableCollection<LineProxy> Lines => _data;

        public InlineViewer(IObservable<ILineProvider> lineProvider, 
            ISchedulerProvider schedulerProvider, 
            IObservable<LineProxy> whenValueChanged)
        {

            var scroller = whenValueChanged.Where(proxy => proxy != null)
                    .ObserveOn(schedulerProvider.Background)
                .CombineLatest(lineProvider, (proxy, lp) =>
                {
                    return new ScrollRequest(this.PageSize, (int) proxy.Number, true);
                });


            _lineScroller = new LineScroller(lineProvider, scroller);


            var pageChanged = this.WhenValueChanged(vm => vm.PageSize);
            var firstChanged = this.WhenValueChanged(vm => vm.PageSize);

            //var scroller = pageChanged.CombineLatest(firstChanged, (page, index) => new ScrollRequest(ScrollReason.User, page, index))
            //.ObserveOn(schedulerProvider.Background)
            //.DistinctUntilChanged();


            //load lines into observable collection
            var loader = _lineScroller.Lines.Connect()
                .Transform(line => new LineProxy(line))
                .Sort(SortExpressionComparer<LineProxy>.Ascending(proxy => proxy))
                .ObserveOn(schedulerProvider.MainThread)
                .Bind(out _data)
                .Subscribe();

            _cleanUp = new CompositeDisposable(_lineScroller,
                        loader,
                        Disposable.Create(() =>
                        {
                            _userScrollRequested.OnCompleted();
                        }));
        }


        void IScrollReceiver.ScrollBoundsChanged(ScrollBoundsArgs boundsArgs)
        {
            if (boundsArgs == null) throw new ArgumentNullException(nameof(boundsArgs));
            _userScrollRequested.OnNext(new ScrollRequest(ScrollReason.User, boundsArgs.PageSize, boundsArgs.FirstIndex));
            PageSize = boundsArgs.PageSize;
            FirstIndex = boundsArgs.FirstIndex;
        }

        void IScrollReceiver.ScrollChanged(ScrollChangedArgs scrollChangedArgs)
        {
        }

        public int PageSize
        {
            get { return _pageSize; }
            set { SetAndRaise(ref _pageSize, value); }
        }

        public int FirstIndex
        {
            get { return _firstIndex; }
            set { SetAndRaise(ref _firstIndex, value); }
        }

        public void Dispose()
        {
            _cleanUp.Dispose();
        }
    }
}